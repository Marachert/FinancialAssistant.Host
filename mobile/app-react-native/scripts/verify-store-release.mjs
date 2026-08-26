import { constants } from 'node:fs';
import { execFile } from 'node:child_process';
import { access, readFile, stat } from 'node:fs/promises';
import { dirname, isAbsolute, relative, resolve, sep } from 'node:path';
import { fileURLToPath } from 'node:url';
import { promisify } from 'node:util';

const root = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const strict = process.argv.includes('--strict');
const failures = [];
const requiredTypes = ['Email Address', 'User ID', 'Other Financial Info', 'Photos or Videos', 'Other User Content'];
const run = promisify(execFile);

async function readJson(path) {
  return JSON.parse(await readFile(resolve(root, path), 'utf8'));
}

function requireValue(condition, message) {
  if (!condition) failures.push(message);
}

function requireHttps(value, label) {
  try {
    const url = new URL(value);
    requireValue(url.protocol === 'https:', `${label} must use HTTPS.`);
  } catch {
    failures.push(`${label} must be a valid URL.`);
  }
}

async function readPng(path, alphaRequired) {
  const fullPath = resolve(root, path);
  const bytes = await readFile(fullPath);
  const details = await stat(fullPath);
  requireValue(bytes.subarray(1, 4).toString('ascii') === 'PNG', `${path} must be a PNG.`);
  const width = bytes.readUInt32BE(16);
  const height = bytes.readUInt32BE(20);
  requireValue(width === height && width >= 1024, `${path} must be square and at least 1024 pixels.`);
  requireValue(details.size <= 2_000_000, `${path} must remain below 2 MB.`);
  if (alphaRequired) requireValue([4, 6].includes(bytes[25]), `${path} must contain an alpha channel.`);
}

async function readGitCandidate() {
  const options = { cwd: root, encoding: 'utf8', windowsHide: true };
  const [repository, commit, tree, status] = await Promise.all([
    run('git', ['rev-parse', '--show-toplevel'], options),
    run('git', ['rev-parse', 'HEAD'], options),
    run('git', ['rev-parse', 'HEAD^{tree}'], options),
    run('git', ['status', '--porcelain', '--untracked-files=no'], options),
  ]);

  return {
    repositoryRoot: repository.stdout.trim(),
    commitSha: commit.stdout.trim(),
    treeSha: tree.stdout.trim(),
    clean: status.stdout.trim().length === 0,
  };
}

async function requireReadableFile(path, label) {
  try {
    await access(path, constants.R_OK);
    requireValue((await stat(path)).isFile(), `${label} must identify a regular file.`);
  } catch {
    failures.push(`${label} must exist and be readable.`);
  }
}

const app = (await readJson('app.json')).expo;
const eas = await readJson('eas.json');
const metadata = await readJson('store/release-metadata.json');
const disclosures = await readJson('store/privacy-disclosures.json');
const template = await readJson('store/console-records.example.json');
const imagePicker = app.plugins.find((plugin) => Array.isArray(plugin) && plugin[0] === 'expo-image-picker')?.[1];

requireValue(app.name === 'Financial Assistant', 'App display name is not canonical.');
requireValue(app.ios?.bundleIdentifier === 'com.financialassistant.mobile', 'iOS bundle identifier is not canonical.');
requireValue(/^\d+$/.test(app.ios?.buildNumber) && Number(app.ios.buildNumber) > 0, 'iOS build number must be positive.');
requireValue(app.ios?.config?.usesNonExemptEncryption === false, 'iOS export-compliance answer must be explicit.');
requireValue(app.android?.package === 'com.financialassistant.mobile', 'Android package name is not canonical.');
requireValue(Number.isInteger(app.android?.versionCode) && app.android.versionCode > 0, 'Android version code must be positive.');
requireValue(eas.cli?.version === '22.4.0', 'EAS CLI version must be pinned.');
requireValue(eas.build?.production?.distribution === 'store', 'Production builds must use store distribution for TestFlight.');
requireValue(eas.build?.production?.android?.buildType === 'app-bundle', 'Google Play builds must use AAB.');
requireValue(eas.submit?.production?.android?.track === 'internal', 'Google Play submission must target the internal track.');
requireValue(eas.submit?.production?.android?.releaseStatus === 'draft', 'Google Play submission must remain draft until owner approval.');

await readPng(app.icon, false);
await readPng(app.android.adaptiveIcon.foregroundImage, true);

requireValue(metadata.identity?.appName.length <= 30, 'Apple app name exceeds 30 characters.');
requireValue(metadata.apple?.subtitle.length <= 30, 'Apple subtitle exceeds 30 characters.');
requireValue(metadata.apple?.promotionalText.length <= 170, 'Apple promotional text exceeds 170 characters.');
requireValue(metadata.apple?.description.length <= 4000, 'Apple description exceeds 4000 characters.');
requireValue(metadata.apple?.keywords.length <= 100, 'Apple keywords exceed 100 characters.');
requireValue(metadata.google?.shortDescription.length <= 80, 'Google short description exceeds 80 characters.');
requireValue(metadata.google?.fullDescription.length <= 4000, 'Google full description exceeds 4000 characters.');
requireValue(metadata.screenshots?.length >= 8, 'Store screenshot checklist is incomplete.');
requireHttps(metadata.urls?.privacyPolicy, 'Privacy policy URL');
requireHttps(metadata.urls?.support, 'Support URL');
requireValue(metadata.permissions?.camera === 'Allow Financial Assistant to photograph a receipt for review.', 'Camera rationale drifted from native config.');
requireValue(metadata.permissions?.photos === 'Allow Financial Assistant to select a receipt image for review.', 'Photo rationale drifted from native config.');
requireValue(imagePicker?.cameraPermission === metadata.permissions?.camera, 'Native camera permission copy does not match store metadata.');
requireValue(imagePicker?.photosPermission === metadata.permissions?.photos, 'Native photo permission copy does not match store metadata.');
requireValue(imagePicker?.microphonePermission === false, 'Receipt image capture must not request microphone access.');

requireValue(disclosures.tracking === false, 'Tracking must remain disabled.');
requireValue(disclosures.thirdPartyAdvertising === false, 'Third-party advertising must remain disabled.');
requireValue(disclosures.dataSharedWithThirdParties === false, 'Data sharing must remain disabled for the controlled POC.');
for (const type of requiredTypes) requireValue(disclosures.apple?.some((item) => item.dataType === type), `Apple disclosure is missing ${type}.`);
requireValue(disclosures.apple?.every((item) => item.tracking === false), 'Apple disclosure cannot mark a data type for tracking.');
requireValue(disclosures.googlePlay?.every((item) => item.shared === false), 'Google Play disclosure cannot mark data as shared.');
requireValue(template.productionApiUrl.startsWith('REQUIRED_'), 'Console template must not contain a usable gateway URL.');
requireValue(template.candidate?.commitSha.startsWith('REQUIRED_') && template.candidate?.treeSha.startsWith('REQUIRED_'), 'Console template must not contain candidate Git identities.');
requireValue(template.googlePlay?.serviceAccountKeyPath.startsWith('REQUIRED_'), 'Console template must not contain a service-account path.');
requireValue(template.apple?.appRecordCreated === false && template.googlePlay?.appRecordCreated === false, 'Console template must not claim external records exist.');

if (strict) {
  const localPath = resolve(root, 'store/console-records.local.json');
  try {
    await access(localPath);
    const local = JSON.parse(await readFile(localPath, 'utf8'));
    let gitCandidate;
    try {
      gitCandidate = await readGitCandidate();
    } catch {
      failures.push('Strict validation requires an accessible Git worktree.');
    }

    requireHttps(local.productionApiUrl, 'Approved production gateway URL');
    requireValue(!local.productionApiUrl.includes('REQUIRED_'), 'Approved gateway URL is still a placeholder.');
    requireValue(/^[0-9a-f-]{36}$/i.test(local.expo?.projectId), 'EAS project ID is missing.');
    requireValue(local.candidate?.version === app.version, 'Console evidence app version does not match app config.');
    requireValue(local.candidate?.iosBuildNumber === app.ios.buildNumber, 'Console evidence iOS build number does not match app config.');
    requireValue(local.candidate?.androidVersionCode === app.android.versionCode, 'Console evidence Android version code does not match app config.');
    if (gitCandidate) {
      requireValue(gitCandidate.clean, 'Tracked worktree changes must be committed before strict validation.');
      requireValue(local.candidate?.commitSha === gitCandidate.commitSha, 'Console evidence commit does not match HEAD.');
      requireValue(local.candidate?.treeSha === gitCandidate.treeSha, 'Console evidence tree does not match HEAD.');
    }
    requireValue(local.apple?.appRecordCreated === true, 'App Store Connect record is not confirmed.');
    requireValue(/^\d+$/.test(local.apple?.appStoreConnectAppId), 'App Store Connect app ID is missing.');
    requireValue(/^[A-Z0-9]{10}$/.test(local.apple?.teamId), 'Apple team ID is missing.');
    requireValue(local.apple?.testFlightInternalGroupCreated === true, 'TestFlight internal group is not confirmed.');
    requireValue(local.apple?.privacyAnswersSaved === true && local.apple?.metadataSaved === true, 'Apple privacy or metadata is incomplete.');
    requireValue(local.googlePlay?.appRecordCreated === true, 'Google Play app record is not confirmed.');
    requireValue(local.googlePlay?.packageName === app.android.package, 'Google Play package does not match app config.');
    requireValue(local.googlePlay?.internalTesterListCreated === true && local.googlePlay?.internalTrackCreated === true, 'Google Play internal testing is not confirmed.');
    requireValue(local.googlePlay?.dataSafetyAnswersSaved === true && local.googlePlay?.metadataSaved === true, 'Google Play data safety or metadata is incomplete.');
    const serviceAccountPath = local.googlePlay?.serviceAccountKeyPath;
    const serviceAccountConfigured = typeof serviceAccountPath === 'string' && !serviceAccountPath.includes('REQUIRED_');
    requireValue(serviceAccountConfigured, 'Google service-account key path is missing.');
    if (serviceAccountConfigured && gitCandidate) {
      const resolvedServiceAccountPath = resolve(serviceAccountPath);
      const repositoryRelativePath = relative(gitCandidate.repositoryRoot, resolvedServiceAccountPath);
      const outsideRepository = isAbsolute(repositoryRelativePath) || repositoryRelativePath === '..' || repositoryRelativePath.startsWith(`..${sep}`);
      requireValue(isAbsolute(serviceAccountPath) && outsideRepository, 'Google service-account key must use an absolute path outside the repository.');
      if (isAbsolute(serviceAccountPath) && outsideRepository) {
        await requireReadableFile(resolvedServiceAccountPath, 'Google service-account key');
      }
    }
    requireValue(local.releaseOwnerApproval === true, 'Release owner approval is missing.');
  } catch (error) {
    if (error?.code === 'ENOENT') failures.push('Strict validation requires ignored store/console-records.local.json.');
    else throw error;
  }
}

if (failures.length > 0) {
  for (const failure of failures) process.stderr.write(`ERROR: ${failure}\n`);
  process.exit(1);
}

process.stdout.write(strict
  ? 'Verified repository configuration and account-owner mobile store gates.\n'
  : 'Verified credential-free mobile store release configuration; strict account-owner gates were not requested.\n');
