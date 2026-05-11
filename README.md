# Control Tools - UI Gradient

Configurable GPU-backed gradient rendering for Unity UI graphics.

## Installation

Add the package through the Unity Package Manager using the local package path or keep the package referenced from a Unity project's `Packages/manifest.json`.

```json
"com.control-tools.ui-gradient": "file:../../com.control-tools.ui-gradient"
```

## Usage

1. Add a `Control/UI Gradient` component to a UI object.
2. Pick a gradient type.
3. Click the preview strip to edit gradient stops in the custom editor window.
4. Configure the start point, end point, and squash point in the Inspector.
5. Use the Scene view handles to position the gradient interactively.

The runtime component renders through a generated UI mesh, a per-instance gradient texture, and the included `Control/UI/Gradient` shader.

## Soft Masks

Unity's built-in `Mask` is stencil-based, so it clips children but does not multiply child alpha by the mask graphic alpha.

For gradient alpha masking:

1. Add `Control/UI Gradient` to the mask object and configure its alpha stops.
2. Add `Control/UI Soft Mask` to the same object.
3. Add `Control/UI Soft Maskable` to UI graphics that should fade by the mask alpha.
4. Assign the `Soft Mask` reference on each `UI Soft Maskable`.

Disable `Show Mask Graphic` on the soft mask when the gradient should only affect children and not draw itself.

The mask texture resolution is calculated automatically from the mask rect size and canvas scale, clamped internally to avoid oversized runtime textures.

`UI Soft Maskable` is driven by its explicit mask reference, not by hierarchy. It works with default Unity UI graphics through the included `Control/UI/SoftMask` shader. Custom shaders need to implement the package soft-mask properties, or they will render without soft masking.

## Notes

- Requires Unity UI (`com.unity.ugui`).
- Uses UI Toolkit and SVG editor icons through Unity's built-in UIElements and VectorGraphics modules.
- The package includes runtime Resources materials so package shaders have serialized references and stay available in player builds.
