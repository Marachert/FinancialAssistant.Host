# Mobile Store Release Tracks

Related Jira: FIN-43.

## Delivered boundary

The repository prepares one store-distribution build profile for iOS TestFlight
and Google Play internal testing. It includes stable identifiers, version codes,
store icon assets, metadata, permission rationales, privacy/data-safety drafts,
and an executable release validator.

Repository validation is credential-free and does not create cloud builds,
submit binaries, enroll developer accounts, or consume paid credits. Apple
Developer enrollment, Google Play registration, Expo/EAS account usage, signing
credentials, store records, tester groups, and submission remain account-owner
actions. Their current fees and quotas must be reviewed and explicitly approved
before running a build or submission command.

## Files

| File | Purpose |
| --- | --- |
| `mobile/app-react-native/app.json` | App identity, semantic/native versions, icons, and native permission copy |
| `mobile/app-react-native/eas.json` | Pinned EAS CLI, store build, TestFlight, and Play internal-draft profiles |
| `mobile/app-react-native/store/release-metadata.json` | App Store and Play listing copy, URLs, review notes, and screenshot checklist |
| `mobile/app-react-native/store/privacy-disclosures.json` | Conservative Apple App Privacy and Google Data safety answer source |
| `mobile/app-react-native/store/console-records.example.json` | Credential-free template for external account evidence |
| `docs/legal/privacy-policy.md` | Public controlled-POC privacy policy |

## Repository gate

From `mobile/app-react-native`:

```powershell
npm ci --no-audit --no-fund
npm run verify
```

`verify` checks TypeScript, lint, mobile structure, PNG dimensions/alpha,
identifiers, versions, EAS profiles, metadata length limits, HTTPS policy/support
URLs, permission-copy consistency, conservative disclosures, and the absence of
false external-record claims. It makes no network mutation.

## Account-owner gate

1. Confirm the approved HTTPS gateway and privacy/support URLs are reachable.
2. Enroll or select authorized Apple, Google Play, and Expo accounts only after
   the release owner approves current fees, quotas, and terms.
3. Before every new candidate, increment `expo.version`,
   `expo.ios.buildNumber`, and `expo.android.versionCode` in `app.json`. Store
   console evidence must match all three values exactly.
4. Create the App Store Connect record for bundle ID
   `com.financialassistant.mobile` and a TestFlight internal tester group.
5. Create the Google Play record for package
   `com.financialassistant.mobile`, an internal tester list, and an internal
   testing track.
6. Configure EAS without committing secrets:

```powershell
npx --yes eas-cli@22.4.0 init
npx --yes eas-cli@22.4.0 env:create --environment production --name EXPO_PUBLIC_API_URL --value https://approved-gateway.example
```

7. Copy `store/console-records.example.json` to the ignored
   `store/console-records.local.json`. Record only non-secret IDs, paths to keys
   outside the repository, completed console states, and owner approval.
8. Reconcile `store/privacy-disclosures.json` against the exact native manifest,
   backend environment, every enabled SDK/provider, and actual retention/deletion
   behavior. Save the answers and listing metadata in both consoles.
9. Run the strict local gate:

```powershell
npm run verify:release -- --strict
```

Strict success is mandatory evidence before any build command. The ignored local
record must never be attached to Jira, Confluence, CI logs, or a pull request.

## Controlled build and submit

Run only after explicit release-owner approval for platform and EAS usage:

```powershell
npx --yes eas-cli@22.4.0 build --platform ios --profile production
npx --yes eas-cli@22.4.0 submit --platform ios --profile production
npx --yes eas-cli@22.4.0 build --platform android --profile production
npx --yes eas-cli@22.4.0 submit --platform android --profile production
```

The iOS production profile uses store distribution, which is required for
TestFlight. The Android profile produces an AAB; submission targets the internal
track with draft status so an owner must review it before rollout. EAS Submit
uploads binaries but does not replace console metadata, screenshots, privacy
answers, tester management, review notes, or release-owner approval.

## Evidence and blockers

Record the same candidate commit, app version/build numbers, EAS build IDs,
submission IDs, processing status, tester groups, CI runs, smoke-test evidence,
and sanitized store screenshots in Jira and Confluence. Do not record tokens,
API keys, signing material, tester identities, gateway secrets, or real financial
data.

The release is Blocked when strict validation fails, either app record or tester
path is missing, URLs are not approved HTTPS, signing is unavailable, privacy or
metadata answers drift from the candidate, required CI is not green, mobile smoke
evidence is incomplete, a paid action lacks approval, or any P0/P1 defect remains.

## Current status

The repository-controlled path is prepared by FIN-43. External store records,
signing, builds, submissions, processing, and tester invitations are deliberately
not claimed until an authorized account owner completes and evidences them. This
separation keeps autonomous delivery restart-safe and prevents unapproved spend.

## Official references

- [Expo EAS build profiles](https://docs.expo.dev/build/eas-json/)
- [Expo TestFlight submission](https://docs.expo.dev/submit/testflight/)
- [Expo app-store submission](https://docs.expo.dev/deploy/submit-to-app-stores/)
- [Apple App Privacy details](https://developer.apple.com/app-store/app-privacy-details/)
- [Google Play Data safety](https://support.google.com/googleplay/android-developer/answer/10787469)
- [Google Play internal testing](https://support.google.com/googleplay/android-developer/answer/9845334)
