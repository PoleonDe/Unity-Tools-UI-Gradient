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

## Notes

- Requires Unity UI (`com.unity.ugui`).
- Uses UI Toolkit and SVG editor icons through Unity's built-in UIElements and VectorGraphics modules.
- The package includes `Runtime/Resources/Materials/UIGradient.mat` so the shader has a serialized Resources reference and is kept available in player builds.
