# FluentT Avatar Controller Sample

Sample avatar controller implementation for FluentT Talkmotion system.

## Features

- **Eye Blink Animation**: Automatic eye blink with random intervals
- **Look Target Control**: Dynamic gaze control with virtual targets and weighted blending
- **Emotion Tagging**: Text-based emotion detection and facial expression control
- **Server Motion Tagging**: Server-driven motion tag responses
- **Body Animation**: State machine-based body animation system

## Installation

### Via Package Manager (Git URL)

1. Open Unity Package Manager (Window > Package Manager)
2. Click `+` button → Add package from git URL
3. Enter: `https://github.com/Fluentt-ai/fluentt-avatar-controller-sample.git`

### Via manifest.json

Add to your project's `Packages/manifest.json`:

```json
{
  "dependencies": {
    "com.fluentt.avatar-controller-sample": "https://github.com/Fluentt-ai/fluentt-avatar-controller-sample.git"
  }
}
```

## Requirements

- Unity 2022.3 or later
- FluentT Talkmotion SDK (`com.fluentt.talkmotion`)

## Usage

1. Add `FluentTAvatarControllerFloatingHead` component to your avatar GameObject
2. Configure the avatar references and settings in the inspector
3. The controller will automatically integrate with the FluentTAvatar component

## Documentation

See the [Documentation](Documentation~/README.md) folder for detailed usage guides.

## License

Proprietary - FluentT AI
