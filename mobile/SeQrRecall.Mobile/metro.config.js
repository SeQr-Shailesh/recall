const path = require('path');
const { getDefaultConfig, mergeConfig } = require('@react-native/metro-config');

const projectRoot = path.resolve(__dirname);

/**
 * Windows: Metro's file map misses files when the repo path contains a space
 * (`SeQr Recall`) and when the crawler walks huge `android/app/.cxx` trees.
 * That shows up as "Failed to get the SHA-1 for ... metro-runtime ... require.js".
 *
 * @type {import('@react-native/metro-config').MetroConfig}
 */
const config = {
  projectRoot,
  resolver: {
    useWatchman: false,
    extraNodeModules: {
      'metro-runtime': path.join(projectRoot, 'node_modules', 'metro-runtime'),
    },
    blockList: [
      /[/\\]__tests__[/\\].*/,
      /[/\\]android[/\\]app[/\\](?:build|\.cxx)[/\\].*/,
      /[/\\]android[/\\]\.gradle[/\\].*/,
    ],
  },
  watcher: {
    unstable_lazySha1: false,
    unstable_autoSaveCache: {
      enabled: false,
    },
  },
  server: {
    // Windows + Android: OkHttp's chunked multipart reader fails with
    // "Expected leading [0-9a-fA-F] character but was 0xd" while Metro
    // streams bundling progress. Serve a single JS response instead.
    enhanceMiddleware: (middleware) => {
      return (req, res, next) => {
        const url = req.url || '';
        if (url.includes('.bundle') && typeof req.headers.accept === 'string') {
          req.headers.accept = req.headers.accept
            .split(',')
            .map((value) => value.trim())
            .filter((value) => !value.toLowerCase().startsWith('multipart/'))
            .join(', ');
        }
        return middleware(req, res, next);
      };
    },
  },
};

module.exports = mergeConfig(getDefaultConfig(projectRoot), config);
