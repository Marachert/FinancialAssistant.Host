const path = require('node:path');
const fs = require('node:fs');
const dependencyRoot = path.dirname(require.resolve('react/package.json'));
module.exports = {
  projectRoot: __dirname,
  watchFolders: [path.dirname(dependencyRoot), fs.realpathSync(path.dirname(dependencyRoot))],
  maxWorkers: 2,
  resolver: {
    nodeModulesPaths: [path.dirname(dependencyRoot)],
    sourceExts: ['js', 'jsx', 'mjs', 'json'],
    resolverMainFields: ['browser', 'main'],
    blockList: /node_modules[/\\]\.[^/\\]+/,
  },
  transformer: { babelTransformerPath: require.resolve('metro-babel-transformer') },
};
