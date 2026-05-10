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
3. Configure the color gradient, start point, end point, and squash point in the Inspector.
4. Use the Scene view handles to position the gradient interactively.

The runtime component renders through a generated UI mesh and the included `Control/UI/Gradient` shader.

## Notes

- Requires Unity UI (`com.unity.ugui`).
- The package includes `Runtime/Resources/Materials/UIGradient.mat` so the shader has a serialized Resources reference and is kept available in player builds.
