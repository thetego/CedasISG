import { DemoProvider } from './demo.js';
import { PlayFabProvider } from './playfab.js';

export function createProvider(config, root) {
  return config.provider === 'playfab'
    ? new PlayFabProvider(config.playfab)
    : new DemoProvider(root, config.demoPassword);
}
