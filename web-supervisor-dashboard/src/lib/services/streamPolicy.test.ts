import { describe, expect, it } from 'vitest';
import { streamBlockReason } from './streamPolicy';
import type { Emergency } from '../../types';

const sos: Emergency = { id: 'e', source: 'sos', status: 'open', timestamp: 1 };

describe('streamBlockReason', () => {
  it('allows an open or acknowledged SOS with consent', () => {
    expect(streamBlockReason(sos, true, false)).toBeNull();
    expect(streamBlockReason({ ...sos, status: 'acknowledged' }, true, false)).toBeNull();
  });

  it('blocks everything else', () => {
    expect(streamBlockReason(sos, false, false)).toMatch(/consent/);
    expect(streamBlockReason({ ...sos, source: 'audio' }, true, false)).toMatch(/SOS/);
    expect(streamBlockReason({ ...sos, source: 'imu' }, true, false)).toMatch(/SOS/);
    expect(streamBlockReason({ ...sos, status: 'resolved' }, true, false)).toMatch(/resolved/);
    expect(streamBlockReason(sos, true, true)).toMatch(/already/);
  });
});
