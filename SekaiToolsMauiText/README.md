# SekaiToolsMauiText Translate Module

This project now includes a MAUI version of the `SekaiToolsGUI` Translate module.

## Implemented Features

- Load story script (`.json` / `.asset`)
- Load translation text (`.txt`)
- Load reference translation (`.txt`) for side-by-side review
- Edit dialog/effect lines in a unified list
- Auto-sync same speaker name translations across dialog lines
- Auto-sync same effect text translations across effect lines
- Dialog punctuation normalization and warning checks
- Length warning logic (`37` / `45` max line length rule)
- Fast-copy panel for special characters
- Add/remove custom fast-copy characters (saved with MAUI `Preferences`)
- Export translation text file

## Main Files

- `SekaiToolsMauiText/View/Translate/TranslatePage.xaml`
- `SekaiToolsMauiText/View/Translate/TranslatePage.xaml.cs`
- `SekaiToolsMauiText/View/Translate/Components/FastCopyView.xaml`
- `SekaiToolsMauiText/View/Translate/Components/TranslateLineDialog.xaml`
- `SekaiToolsMauiText/View/Translate/Components/TranslateLineEffect.xaml`
- `SekaiToolsMauiText/ViewModel/TranslatePageModel.cs`
- `SekaiToolsMauiText/ViewModel/LineDialogModel.cs`
- `SekaiToolsMauiText/ViewModel/LineEffectModel.cs`
- `SekaiToolsMauiText/ViewModel/LineModel.cs`

## Quick Run (Windows)

```powershell
cd "E:\ProjectSekai\SekaiSubtitle\SekaiTools"
dotnet build SekaiToolsMauiText/SekaiToolsMauiText.csproj -f net10.0-windows10.0.19041.0
```

Optional launch:

```powershell
cd "E:\ProjectSekai\SekaiSubtitle\SekaiTools"
dotnet build SekaiToolsMauiText/SekaiToolsMauiText.csproj -t:Run -f net10.0-windows10.0.19041.0
```

## Smoke Test Checklist

1. Open app, enter Translate page.
2. Click `载入剧本`, select a valid script file.
3. Confirm line list renders dialog/effect rows.
4. Click `打开翻译`, load a matching `.txt` and confirm values populate.
5. Click `打开对照翻译`, verify reference text appears in rows.
6. Edit a speaker name and verify same original speaker lines sync.
7. Edit an effect translation and verify same original effect lines sync.
8. Click special-character buttons and confirm clipboard updates.
9. Save output and verify exported text file content.

