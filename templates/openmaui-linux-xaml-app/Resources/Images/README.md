# Image Resources

## Required Images

### dotnet_bot.png
Download from the official .NET MAUI repository:
https://github.com/dotnet/maui/blob/main/src/Templates/src/templates/maui-mobile/Resources/Images/dotnet_bot.png

Or use your own application icon.

## Adding Images

Place images in this folder and reference them in XAML:

```xml
<Image Source="dotnet_bot.png" WidthRequest="250" HeightRequest="310" />
```

Images are automatically included via the project file:
```xml
<MauiImage Include="Resources\Images\*" />
```
