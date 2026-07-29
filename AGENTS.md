## Unity CLI licensing recovery

If a Unity batchmode test run repeatedly logs `Channel LicenseClient-<user> doesn't exist`, 60-second licensing timeouts, unknown package entitlements, or `'com.unity.editor.headless' was not found`, do not wait indefinitely or reactivate/delete the license first. Existing GUI editors can remain healthy while a fresh direct CLI launch is blocked by an orphaned generic licensing client.

1. Stop the stuck batchmode Unity process.
2. Inspect licensing processes outside the sandbox:
   ```sh
   pgrep -alf 'Unity|Licensing|Unity Hub'
   ps -p <candidate-pids> -o pid,ppid,lstart,etime,state,command
   ```
3. Identify the **generic** editor licensing client whose command ends with `--namedPipe Unity-LicenseClient-<user>`. Treat it as orphaned only when its parent is PID 1, the licensing log reports `Failed to acquire global mutex ... Another instance ... is already running`, Unity cannot connect to that channel, and no live Unity editor command line references that generic channel through `-licensingIpc LicenseClient-<user>`. If a live editor references it, do not kill it; close that editor cleanly first or restart Hub after closing editors.
4. Terminate only that confirmed orphan with `kill <pid>`. Do **not** terminate:
   - the Unity Hub process;
   - Hub's licensing client under `Unity Hub.app`;
   - a running editor's version-specific client such as `--namedPipe Unity-LicenseClient-<user>-6000.5.4`;
   - any open Unity editor.
5. Retry the focused batchmode command and inspect its log promptly. Recovery is confirmed by messages equivalent to:
   - `Successfully connected to: "LicenseClient-<user>"`;
   - Licensing Client protocol/version matching the editor;
   - the expected entitlement group resolving;
   - `Licensing is initialized` within seconds.

Useful logs:
- `~/Library/Logs/Unity/Unity.Licensing.Client.log`
- the command-specific Unity `-logFile`

A Hub/editor licensing protocol split can contribute to this failure. If orphan cleanup does not hold, restart or update Unity Hub after closing editors cleanly. Preserve `~/Library/Unity/licenses/UnityEntitlementLicense.xml` unless separate evidence shows the entitlement itself is invalid.
