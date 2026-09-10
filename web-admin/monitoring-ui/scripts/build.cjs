const fs = require('node:fs/promises');
const path = require('node:path');
const Metro = require('metro');
const root = path.resolve(__dirname, '..');
async function build() {
  await fs.mkdir(path.join(root, 'dist/icons'), {
    recursive: true
  });
  const config = await Metro.loadConfig({
    config: path.join(root, 'metro.config.cjs'),
    cwd: root
  });
  await Metro.runBuild(config, {
    entry: 'src/app/index.jsx',
    out: path.join(root, 'dist/app.js'),
    platform: 'web',
    dev: false,
    minify: true,
    sourceMap: false
  });
  for (const file of ['index.html', 'styles.css']) {
    await fs.copyFile(path.join(root, 'public', file), path.join(root, 'dist', file));
  }
  const notices = [];
  for (const name of ['react', 'react-dom', 'lucide']) {
    const directory = path.dirname(require.resolve(name + '/package.json'));
    notices.push(name + '\n\n' + (await fs.readFile(path.join(directory, 'LICENSE'), 'utf8')));
  }
  await fs.writeFile(path.join(root, 'dist/THIRD-PARTY-NOTICES.txt'), notices.join('\n\n'));
  const {
    icons
  } = require('lucide');
  for (const name of ['Activity', 'Server', 'ListChecks', 'TriangleAlert', 'ChartNoAxesCombined', 'LifeBuoy', 'RefreshCw', 'LogOut', 'Search', 'ShieldCheck']) {
    const nodes = icons[name];
    if (!nodes) throw new Error('Required icon missing: ' + name);
    const content = nodes.map(([tag, attrs]) => `<${tag} ${Object.entries(attrs).filter(([key]) => key !== 'key').map(([key, value]) => `${key}="${value}"`).join(' ')}/>`).join('');
    await fs.writeFile(path.join(root, 'dist/icons', name + '.svg'), `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="#344547" stroke-width="1.7" stroke-linecap="round" stroke-linejoin="round">${content}</svg>`);
  }
  console.log('Monitoring web build complete.');
}
build().catch(error => {
  console.error('Monitoring web build failed:', error.message);
  process.exitCode = 1;
});
