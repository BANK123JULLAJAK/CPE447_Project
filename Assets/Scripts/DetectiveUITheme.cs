using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Detective Evidence Board UI Kit Theme (1888 Scotland Yard Aesthetic)
/// Inspired by https://nexavisuals.itch.io/detective-evidence-board-ui-kit
/// Uses authentic high-res textures:
/// - Mahogany Framed Corkboard Background
/// - 1888 Police Department Case Dossier Folder
/// - Aged Parchment Index Clue Cards
/// - Red Thumbtacks / Pushpins & Crimson Connecting Strings
/// </summary>
public static class DetectiveUITheme
{
    private static Sprite _corkboardBg;
    private static Sprite _caseFolder;
    private static Sprite _noteCard;
    private static Sprite _redPin;

    public static Sprite CorkboardBg
    {
        get
        {
            if (_corkboardBg == null) _corkboardBg = LoadSprite("Assets/Sprites/DetectiveUI/DetectiveCorkboardBg.jpg");
            return _corkboardBg;
        }
    }

    public static Sprite CaseFolder
    {
        get
        {
            if (_caseFolder == null) _caseFolder = LoadSprite("Assets/Sprites/DetectiveUI/DetectiveCaseFolder.jpg");
            return _caseFolder;
        }
    }

    public static Sprite NoteCard
    {
        get
        {
            if (_noteCard == null) _noteCard = LoadSprite("Assets/Sprites/DetectiveUI/DetectiveNoteCard.jpg");
            return _noteCard;
        }
    }

    public static Sprite RedPin
    {
        get
        {
            if (_redPin == null) _redPin = LoadSprite("Assets/Sprites/DetectiveUI/DetectiveRedPin.jpg");
            return _redPin;
        }
    }

    private static Sprite _sepiaBlueprint;

    public static Sprite SepiaBlueprint
    {
        get
        {
            if (_sepiaBlueprint == null) _sepiaBlueprint = LoadSprite("Assets/Sprites/DetectiveUI/VictorianSepiaBlueprint.jpg");
            return _sepiaBlueprint;
        }
    }

    // Color Palette matching 1888 Victorian Detective Noir
    public static readonly Color BrassGold = new Color(0.71f, 0.60f, 0.34f, 1f); // #B59A57 Victorian Brass
    public static readonly Color AgedPaper = new Color(0.91f, 0.86f, 0.72f, 1f); // #E8DCB8 Aged Paper
    public static readonly Color OldInk = new Color(0.17f, 0.13f, 0.09f, 1f); // #2B2118 Vintage Ink
    public static readonly Color ManilaFolder = new Color(0.82f, 0.73f, 0.58f, 1f);
    public static readonly Color RedYarnString = new Color(0.88f, 0.14f, 0.14f, 0.95f);
    public static readonly Color RedYarnGlow = new Color(1.0f, 0.30f, 0.25f, 0.5f);
    public static readonly Color DarkBackdrop = new Color(0.06f, 0.05f, 0.04f, 0.94f); // Rich dark sepia vignette
    public static readonly Color InkDark = new Color(0.14f, 0.10f, 0.07f, 1f); // Aged fountain pen ink
    public static readonly Color InkMuted = new Color(0.35f, 0.26f, 0.18f, 1f); // Faded ink
    public static readonly Color GoldAccent = new Color(0.85f, 0.65f, 0.25f, 1f); // Brass/Gold
    public static readonly Color StampRed = new Color(0.72f, 0.12f, 0.12f, 0.95f); // Confidential ink stamp
    public static readonly Color DiscardGray = new Color(0.40f, 0.38f, 0.35f, 0.70f);

    private static Sprite LoadSprite(string path)
    {
        var spr = GameAssets.Load<Sprite>(path);
        if (spr != null) return spr;
        var tex = GameAssets.Load<Texture2D>(path);
        if (tex != null)
        {
            return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
        }
        return null;
    }
}
