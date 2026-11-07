using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace BowArc
{
    // Client-only. While the local player draws a bow (Humanoid.GetAttackDrawPercentage) past a
    // threshold, we predict the arrow's flight with the same maths vanilla uses and draw it as a
    // faint dotted line:
    //   Attack.FireProjectileBurst()       -> speed = Lerp(velMin, vel, drawVelocityCurve(draw%)),
    //                                         weapon + ammo values added together
    //   Attack.GetProjectileSpawnPoint()   -> origin joint + up*attackHeight + forward*attackRange
    //                                         + right*attackOffset, aimed along GetAimDir()
    //   Projectile.FixedUpdate()           -> vel += down*gravity*dt; vel -= vel^2*drag*dt
    // The random accuracy spread isn't shown: the line is the centre of where the arrow can land.
    // Nothing is patched and nothing is sent to the server.
    [BepInPlugin(Guid, Name, Version)]
    [BepInProcess("valheim.exe")]
    public class Plugin : BaseUnityPlugin
    {
        public const string Guid = "jordan.valheim.bowarc";
        public const string Name = "Bow Arc";
        public const string Version = "0.1.1";

        internal static Plugin Instance;
        internal static ConfigEntry<bool> Enabled;
        internal static ConfigEntry<float> ShowAtDraw;
        internal static ConfigEntry<Color> LineColor;
        internal static ConfigEntry<float> FullDrawAlpha;
        internal static ConfigEntry<float> DotSize;
        internal static ConfigEntry<float> DotSpacing;
        internal static ConfigEntry<float> MaxDistance;
        internal static ConfigEntry<bool> ShowImpactMarker;

        internal static ConfigEntry<bool> ShowAmmoIcon;
        internal static ConfigEntry<float> AmmoIconSeconds;
        internal static ConfigEntry<bool> AmmoIconFlash;
        internal static ConfigEntry<float> AmmoIconFlashSeconds;
        internal static ConfigEntry<float> AmmoIconFlashRate;
        internal static ConfigEntry<float> AmmoIconSize;
        internal static ConfigEntry<float> AmmoIconOffset;

        private TrajectoryLine _line;
        private AmmoIcon _icon;
        private Harmony _harmony;

        private void Awake()
        {
            Instance = this;

            Enabled = Config.Bind("General", "Enabled", true,
                "Show a dotted arc predicting where your arrow will fly while you draw a bow.");
            ShowAtDraw = Config.Bind("General", "ShowAtDrawPercent", 0.05f,
                new ConfigDescription("How far the bow must be drawn (0-1) before the arc appears.",
                    new AcceptableValueRange<float>(0f, 1f)));
            MaxDistance = Config.Bind("General", "MaxDistance", 150f,
                new ConfigDescription("Longest arc to draw, in metres.", new AcceptableValueRange<float>(10f, 500f)));

            LineColor = Config.Bind("Look", "Color", new Color(1f, 1f, 1f, 0.35f),
                "Colour of the dots. The alpha is how see-through the arc is when it first appears.");
            FullDrawAlpha = Config.Bind("Look", "FullDrawAlpha", 0.55f,
                new ConfigDescription("Alpha of the dots once the bow is fully drawn.", new AcceptableValueRange<float>(0f, 1f)));
            DotSize = Config.Bind("Look", "DotSize", 0.06f,
                new ConfigDescription("Dot diameter in metres.", new AcceptableValueRange<float>(0.01f, 0.5f)));
            DotSpacing = Config.Bind("Look", "DotSpacing", 0.5f,
                new ConfigDescription("Distance between dots in metres.", new AcceptableValueRange<float>(0.1f, 5f)));
            ShowImpactMarker = Config.Bind("Look", "ShowImpactMarker", true,
                "Draw a small ring where the arc hits something.");

            ShowAmmoIcon = Config.Bind("Ammo Icon", "Enabled", true,
                "Briefly show the icon of your equipped arrows above the crosshair when you start drawing.");
            AmmoIconSeconds = Config.Bind("Ammo Icon", "Seconds", 1.0f,
                new ConfigDescription("How long the icon stays fully visible before fading.", new AcceptableValueRange<float>(0.1f, 10f)));
            AmmoIconFlash = Config.Bind("Ammo Icon", "Flash", false,
                "Flash the icon on and off instead of showing it steadily.");
            AmmoIconFlashSeconds = Config.Bind("Ammo Icon", "FlashSeconds", 1.5f,
                new ConfigDescription("When Flash is on: how long the icon keeps flashing before it disappears (Seconds is then ignored).",
                    new AcceptableValueRange<float>(0.1f, 10f)));
            AmmoIconFlashRate = Config.Bind("Ammo Icon", "FlashesPerSecond", 4f,
                new ConfigDescription("When Flash is on: how many times per second the icon flashes.",
                    new AcceptableValueRange<float>(0.5f, 20f)));
            AmmoIconSize = Config.Bind("Ammo Icon", "Size", 80f,
                new ConfigDescription("Icon size in pixels.", new AcceptableValueRange<float>(8f, 256f)));
            AmmoIconOffset = Config.Bind("Ammo Icon", "OffsetAboveCrosshair", 45f,
                "How far above the crosshair the icon sits, in pixels.");

            _line = gameObject.AddComponent<TrajectoryLine>();
            _icon = gameObject.AddComponent<AmmoIcon>();

            _harmony = new Harmony(Guid);
            _harmony.PatchAll(typeof(Plugin).Assembly);
        }

        private void OnDestroy()
        {
            _harmony?.UnpatchSelf();
            if (_line) Destroy(_line);
            if (_icon) Destroy(_icon);
        }

        internal static void Log(string msg) => Instance?.Logger.LogInfo(msg);
        internal static void Warn(string msg) => Instance?.Logger.LogWarning(msg);
    }
}
