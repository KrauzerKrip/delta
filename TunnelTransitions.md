# Camera-timed tunnel transitions

Enter the transition trigger while the close camera zone flies the camera into the tunnel. The player and camera transfer together once that close shot settles. There is no seam-plane crossing, required anchor orientation, fade, or movement pause.

## Editor setup

1. Place matching tunnel copies in the same scene. Match their geometry, orientation, scale, lighting, and decoration around the transfer area.
2. Keep `HallAnchor` and `LabsAnchor` at corresponding local points in their tunnel copies. Assign them as **Seam Anchor** on the respective transitions. These are translation reference points only; their rotations do not control timing. If the remote tunnel is translated by 1170 units in X, the reference points must differ by the same amount.
3. Add a **BoxCollider** with **Is Trigger** enabled and **TunnelTransition** to each entry area. Link the **Paired Transition** properties reciprocally. Each box must contain the player's feet/root throughout the camera approach, and the remote box must contain the corresponding translated position. Put the entry volumes in the short stem of the T, before its junction; the branches do not need separate seams.
4. Assign **Close Camera Zone** to the close shot for that tunnel. The shot must win the normal camera-zone priority selection. The destination close zone needs a box trigger containing the corresponding player position. Match the relative camera anchors and shot/follow settings between the copies.
5. Leave **Camera Position Tolerance** at **8** units initially. Increase it to transfer slightly sooner, or decrease it to wait for a closer match. Exponential camera blending approaches its target without reaching exact equality, so a tolerance is required.
6. Place a wider **CameraZone** outside each exit. For example, give the close zone priority 1 and the wider zone priority 0, overlapping the end of the close zone. Exiting the close volume lets the wider shot blend in. Set this up on both sides for hall-to-lab and lab-to-hall travel.

The settled check requires the assigned close zone to be active, position error within the configured tolerance, camera rotation within 2 degrees, and field of view within 1 degree. It checks the camera's fixed axes against its current target. Following axes keep tracking the player and do not block transfer while walking. If all three axes follow, it checks the full position error instead. To wait for the entire shot's exact anchored position, disable following on that shot.

Leaving the transition trigger before the shot settles cancels the pending transfer. Arrival blocks the destination transition until the player leaves its entry trigger. After leaving and re-entering, the return transition waits for its close camera shot in the same way. Returning without first leaving the destination entry volume will not teleport, avoiding immediate bounce-back while the translated camera is already settled.

**Direction** from the earlier seam implementation is no longer used. Existing reference-point assignments and reciprocal links still work. Ordinary camera-zone entry/exit uses the engine's native trigger contacts again. Only teleport handoffs clear source contacts and temporarily seed destination zones until normal contacts arrive. Teleports also clear the player and camera transform interpolation histories so rendering snaps to the translated poses instead of blending back through the old tunnel.

## Current saved scene

The reciprocal links and reference-point separation are now correct. `LabsAnchor` is 1170 units to the right of `HallAnchor`. However, `LabsTransition` still has local X 1161.09399; setting it to 1170 would align its trigger with the corresponding hall trigger. The close labs camera zone is offset by 1168 rather than 1170; increasing its local X by 2 (to 1689.8147) aligns the matching shot and avoids a small camera correction after transfer. These placement edits are left to the editor so the saved scene does not overwrite your active scene.

## Verification

Run `dotnet test UnitTests/TunnelTransitions.Tests.csproj`. Tests cover waiting for the camera, cancellation on departure, arrival suppression, and re-entry for the return trip. They do not simulate engine physics or rendering.

In the s&box editor, verify ordinary camera zones first. Then walk and run into each entry trigger: the camera must approach its close shot before teleportation, maintain its framing across the transfer, and blend wider at either exit. Walk away before the camera settles to confirm cancellation. Remain in the destination entry box to confirm there is no bounce-back, then leave and re-enter it to test the return. Check configuration warnings for missing endpoints, camera zones, or destination coverage.
