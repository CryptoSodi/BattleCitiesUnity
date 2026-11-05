import { BrowserSDK, AddressType } from '@phantom/browser-sdk';
import { createPhantomBridge } from './phantom-core.mjs';

window.BattleCitiesPhantom = createPhantomBridge({ BrowserSDK, AddressType });
