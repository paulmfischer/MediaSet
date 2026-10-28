import * as assert from 'remix/assert';
import { render } from 'remix/component/test';
import { describe, it } from 'remix/test';

import { MobileMenu } from './mobile-menu.tsx';

describe('MobileMenu', () => {
  it('toggles on click and closes on outside mousedown', async (t) => {
    let { $, act, cleanup } = render(<MobileMenu links={[['Home', '/']]} />);
    t.after(cleanup);

    assert.equal($('nav'), null);
    await act(() => $('button')?.click());
    assert.ok($('nav'));
    assert.equal($('button')?.getAttribute('aria-expanded'), 'true');

    await act(() => document.body.dispatchEvent(new MouseEvent('mousedown', { bubbles: true })));
    assert.equal($('nav'), null);
  });
});
