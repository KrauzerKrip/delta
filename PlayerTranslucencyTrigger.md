# Corridor wall translucency

1. Create an empty object covering the corridor where the walls should become translucent.
2. Add a **BoxCollider**, enable **Is Trigger**, and size it to cover the player's path. Add **PlayerTranslucencyTrigger** to the same object. Existing collider shapes also work; multiple triggers on the object are treated as one occupied area.
3. Add the hall wall objects to **Targets** in the Inspector. Drag the wall GameObjects, not their materials. Mesh Components, Model Renderers, and Skinned Model Renderers are supported.
4. Keep **Include Children** enabled to include trim and other renderers beneath each selected wall. Disable it to affect only components directly on the selected objects. Select wall roots rather than the entire hall if doors and floors should stay visible.
5. Start with **Opacity = 0.15**. Zero makes the targets invisible; one preserves their original opacity. The value multiplies the original tint alpha while preserving its RGB color. Leave the walls' normal editor tints at their intended opaque values before play; a tint already set to low alpha will be multiplied again.

The selected objects become translucent as the local player enters and recover their original tints as the last player collider leaves. Collision is unchanged. Disabling or destroying the trigger restores its objects. Overlapping occupied triggers combine using the lowest requested opacity and restore the original tint only after the final request is released. Physics contact lists reconcile occupancy when a player teleports away or a collider is destroyed.

This component changes visibility only. It does not reposition cameras or change teleport timing. Continue using your camera zones and camera-timed TunnelTransition for those behaviors.

In the editor, verify that the exit door is visible through the faded walls from the farther camera position, then walk out or teleport away and check that the walls recover. Also check entering with multiple player colliders and overlapping trigger areas. As with the manual tint shown in the screenshot, the chosen materials need to render tint alpha; if a material ignores alpha, changing the tint will not make that surface translucent.
