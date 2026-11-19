#!/usr/bin/env python3
"""Run the isolated suite and retain durable local history, even on failure."""
import datetime
import json
import os
from pathlib import Path
import shutil
import subprocess
import tempfile
import sys
import uuid
import xml.etree.ElementTree as ET

root = Path(__file__).resolve().parent.parent
run_id = uuid.uuid4().hex
stamp = datetime.datetime.now(datetime.timezone.utc).strftime('%Y%m%dT%H%M%SZ')
output = root / 'artifacts' / 'image-lookup-regressions' / f'{stamp}-{run_id}'
output.mkdir(parents=True)
container = f'mediaset-image-regression-{run_id}'
images = Path(tempfile.gettempdir()) / f'mediaset-images-{run_id}'
commit = subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=root, text=True).strip()
summary = {'runId': run_id, 'commit': commit, 'startedAt': stamp, 'complete': False,
           'scenarios': [], 'infrastructureFailures': [], 'workingTreeDirty': bool(subprocess.check_output(
               ['git', 'status', '--porcelain'], cwd=root, text=True).strip())}
started = datetime.datetime.now(datetime.timezone.utc)


def progress(event, details):
    with (output / 'progress.jsonl').open('a') as stream:
        stream.write(json.dumps({'utc': datetime.datetime.now(datetime.timezone.utc).isoformat(),
                                 'event': event, 'details': details}) + '\n')


def checkpoint():
    (output / 'summary.json').write_text(json.dumps(summary, indent=2) + '\n')


checkpoint()
progress('run-start', summary)
print(f'Artifacts: {output}', flush=True)
env = dict(os.environ, IMAGE_REGRESSION_ARTIFACTS=str(output), IMAGE_REGRESSION_CONTAINER=container,
           IMAGE_REGRESSION_IMAGES=str(images), IMAGE_REGRESSION_RUN_ID=run_id)
exit_code = 1
try:
    with (output / 'test-output.log').open('w') as log:
        result = subprocess.run(['dotnet', 'test', 'MediaSet.ImageLookup.Regressions/MediaSet.ImageLookup.Regressions.csproj',
                                 '--logger', 'trx;LogFileName=results.trx', '--results-directory', str(output)],
                                cwd=root, env=env, stdout=log, stderr=subprocess.STDOUT)
        exit_code = result.returncode
    trx = output / 'results.trx'
    if trx.exists():
        doc = ET.parse(trx)
        ns = {'t': 'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'}
        fixture_ids = {}
        for line in (output / 'progress.jsonl').read_text().splitlines():
            try:
                entry = json.loads(line)
                if entry.get('event') == 'http-fixture':
                    fixture_ids.setdefault(entry['scenarioId'], set()).add(entry['details']['Id'])
            except (ValueError, KeyError):
                continue  # A partial trailing line is evidence of an incomplete/interrupted write.
        for item in doc.findall('.//t:UnitTestResult', ns):
            summary['scenarios'].append({'id': item.attrib['testName'], 'outcome': item.attrib['outcome'],
                                         'duration': item.attrib.get('duration'),
                                         'fixtureIds': sorted(fixture_ids.get(item.attrib['testName'], [])),
                                         'fixtures': ['Fixtures/book-success.json'] if item.attrib['testName'].startswith('BookSuccess_') else ['inline-book'] if item.attrib['testName'].startswith('ProcessBook_') else ['inline-game'] if item.attrib['testName'].startswith('ProcessGame_') else ['inline-music'] if item.attrib['testName'].startswith('ProcessMusic_') else ['inline-recovery'] if item.attrib['testName'].startswith('ProcessRecovery_') else ['inline-contract'],
                                         'failure': ''.join(item.itertext()).strip()})
        result_summary = doc.find('.//t:ResultSummary', ns)
        counters = doc.find('.//t:Counters', ns)
        summary['complete'] = (bool(summary['scenarios'])
                               and all(s['outcome'] != 'NotExecuted' for s in summary['scenarios'])
                               and (result_summary is None or result_summary.attrib.get('outcome') != 'Aborted')
                               and (counters is None or counters.attrib.get('total') == counters.attrib.get('executed')))
    else:
        summary['infrastructureFailures'].append('No test result file; inspect test-output.log')
except BaseException as error:
    summary['infrastructureFailures'].append(str(error))
    progress('run-interrupted', str(error))
finally:
    # Exact unique suite name only. This never inspects, reuses, or removes shared services.
    runtime = env.get('IMAGE_REGRESSION_RUNTIME', 'podman')
    try:
        if runtime == 'podman':
            runtime_args = ['podman', '--remote=false']
        elif runtime == 'docker':
            endpoint = env.get('DOCKER_HOST', 'unix:///var/run/docker.sock')
            if not endpoint.startswith('unix:///'):
                raise OSError('Docker must use a local Unix socket')
            runtime_args = ['docker', '--host', endpoint]
        else:
            raise OSError('Only local podman or docker runtimes are supported')
        cleanup = subprocess.run(runtime_args + ['rm', '--force', '--volumes', container], capture_output=True, text=True)
        progress('cleanup-backstop', {'exit': cleanup.returncode, 'output': cleanup.stdout + cleanup.stderr})
    except OSError as error:
        summary['infrastructureFailures'].append(str(error))
    if images.exists():
        shutil.rmtree(images)
    progress('images-cleanup-backstop', str(images))
    if not summary['complete'] and not summary['infrastructureFailures']:
        summary['infrastructureFailures'].append('Test results do not establish a completed run')
    if (not summary['complete'] or summary['infrastructureFailures']
            or any(scenario['outcome'] != 'Passed' for scenario in summary['scenarios'])):
        exit_code = exit_code or 1
    summary['exitCode'] = exit_code
    summary['durationSeconds'] = (datetime.datetime.now(datetime.timezone.utc) - started).total_seconds()
    checkpoint()
    try:
        lines = [f'# Image lookup regression run {run_id}', '', f'Commit: {commit}',
                 f'Complete: {summary["complete"]}; exit code: {exit_code}',
                 f'Duration: {summary["durationSeconds"]:.3f}s', '',
                 'Fixtures: MediaSet.ImageLookup.Regressions/Fixtures/book-success.json', '',
                 '| Stable scenario ID | Outcome | Duration | Fixtures |', '| --- | --- | --- | --- |']
        for scenario in summary['scenarios']:
            lines.append(f'| {scenario["id"]} | {scenario["outcome"]} | {scenario["duration"]} | {', '.join(scenario['fixtures'] + scenario['fixtureIds'])} |')
        for scenario in summary['scenarios']:
            if scenario['failure']:
                lines.extend(['', f'## {scenario["id"]}', '', '```text', scenario['failure'], '```'])
        lines.extend(['', 'Infrastructure failures: ' + json.dumps(summary['infrastructureFailures']), '',
                      'See results.trx, test-output.log and progress.jsonl for retained evidence.'])
        (output / 'report.md').write_text('\n'.join(lines) + '\n')
    except Exception as error:
        progress('report-finalization-failed', str(error))
        exit_code = 1
        summary['exitCode'] = exit_code
        summary['complete'] = False
        summary['infrastructureFailures'].append('Report finalization failed: ' + str(error))
        checkpoint()
    progress('run-finished', {'exitCode': exit_code, 'complete': summary['complete']})
print((output / 'summary.json').read_text(), flush=True)
sys.exit(exit_code)
