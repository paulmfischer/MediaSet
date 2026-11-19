"""Regression checks for runner failure handling; no containers or providers used."""
import contextlib
import io
import json
from pathlib import Path
import runpy
import subprocess
import tempfile
import unittest
from unittest.mock import patch


class RunnerTests(unittest.TestCase):
    def run_runner(self, mode, expected_exit=1):
        source = Path(__file__).with_name('image-lookup-regressions.py').read_text()
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            script = root / 'scripts' / 'image-lookup-regressions.py'
            script.parent.mkdir()
            script.write_text(source)
            removed = []
            original_write = Path.write_text

            def write_text(path, content, *args, **kwargs):
                if mode == 'report-failure' and path.name == 'report.md':
                    raise OSError('simulated report failure')
                return original_write(path, content, *args, **kwargs)

            def run(command, **kwargs):
                if command[0] == 'dotnet':
                    output = Path(kwargs['env']['IMAGE_REGRESSION_ARTIFACTS'])
                    images = Path(kwargs['env']['IMAGE_REGRESSION_IMAGES'])
                    images.mkdir()
                    (images / 'temporary.png').write_bytes(b'image')
                    kwargs['stdout'].write('retained raw test output\n')
                    with (output / 'progress.jsonl').open('a') as progress:
                        progress.write('{"event":"scenario-start"}\n')
                    if mode == 'interrupted':
                        raise KeyboardInterrupt()
                    if mode == 'missing-trx':
                        return subprocess.CompletedProcess(command, 0)
                    if mode == 'malformed-trx':
                        (output / 'results.trx').write_text('<TestRun>invalid')
                        return subprocess.CompletedProcess(command, 0)
                    outcome = 'Failed' if mode == 'assertion-failure' else 'Passed'
                    result_summary = '<ResultSummary outcome="Aborted" />' if mode == 'aborted-trx' else ''
                    (output / 'results.trx').write_text(
                        '<TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010">'
                        f'<Results><UnitTestResult testName="BookSuccess_fixture" outcome="{outcome}" duration="00:00:01">'
                        '<Output><ErrorInfo><Message>failure evidence</Message></ErrorInfo></Output>'
                        f'</UnitTestResult></Results>{result_summary}</TestRun>')
                    return subprocess.CompletedProcess(command, 1 if outcome == 'Failed' else 0)
                removed.append(command)
                return subprocess.CompletedProcess(command, 0, '', '')

            with patch('subprocess.check_output', return_value='fixture-commit\n'), \
                    patch('subprocess.run', side_effect=run), \
                    patch.object(Path, 'write_text', write_text), \
                    contextlib.redirect_stdout(io.StringIO()):
                with self.assertRaises(SystemExit) as exited:
                    runpy.run_path(str(script), run_name='__main__')
            artifacts = list((root / 'artifacts' / 'image-lookup-regressions').iterdir())
            self.assertEqual(len(artifacts), 1)
            output = artifacts[0]
            summary = json.loads((output / 'summary.json').read_text())
            self.assertEqual(exited.exception.code, expected_exit)
            self.assertEqual(summary['exitCode'], expected_exit)
            self.assertEqual(summary['commit'], 'fixture-commit')
            self.assertTrue((output / 'test-output.log').exists())
            self.assertIn('run-start', (output / 'progress.jsonl').read_text())
            self.assertEqual(len(removed), 1)
            self.assertRegex(removed[0][-1], r'^mediaset-image-regression-[a-f0-9]{32}$')
            self.assertFalse((Path(tempfile.gettempdir()) / removed[0][-1].replace('image-regression', 'images')).exists())
            if mode in ('interrupted', 'missing-trx', 'malformed-trx'):
                self.assertFalse(summary['complete'])
                self.assertEqual((output / 'results.trx').exists(), mode == 'malformed-trx')
            else:
                self.assertTrue((output / 'results.trx').exists())
                self.assertEqual(summary['scenarios'][0]['id'], 'BookSuccess_fixture')
                self.assertEqual(summary['scenarios'][0]['fixtures'], ['Fixtures/book-success.json'])
                self.assertEqual(summary['scenarios'][0]['failure'], 'failure evidence')
            return summary

    def test_zero_subprocess_exit_without_valid_completed_results_fails(self):
        for mode in ('missing-trx', 'malformed-trx', 'aborted-trx'):
            with self.subTest(mode=mode):
                summary = self.run_runner(mode)
                self.assertFalse(summary['complete'])
                self.assertTrue(summary['infrastructureFailures'])

    def test_zero_subprocess_exit_with_completed_passing_results_succeeds(self):
        summary = self.run_runner('success', expected_exit=0)
        self.assertTrue(summary['complete'])
        self.assertEqual(summary['infrastructureFailures'], [])

    def test_failed_assertion_retains_results_and_cleans_owned_resources(self):
        summary = self.run_runner('assertion-failure')
        self.assertTrue(summary['complete'])
        self.assertEqual(summary['scenarios'][0]['outcome'], 'Failed')

    def test_interruption_marks_run_incomplete_and_cleans_owned_resources(self):
        self.run_runner('interrupted')

    def test_report_failure_preserves_raw_evidence_and_marks_incomplete(self):
        summary = self.run_runner('report-failure')
        self.assertFalse(summary['complete'])
        self.assertIn('Report finalization failed', summary['infrastructureFailures'][0])


if __name__ == '__main__':
    unittest.main()
