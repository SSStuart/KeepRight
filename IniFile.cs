using Rage;

namespace KeepRight
{
    internal static class Settings
    {
        internal static bool DEBUG = false;
        internal static float CHECK_FREQUENCY = 0.5f;
        internal static bool DISABLE_FOR_OTHER_EMERGENCY_VEHICLES = false;
        internal static bool ENABLE_BIG_VEHICLE_SIREN_ON = true;
        internal static float WEIGHT_HORN = 2;
        internal static int WEIGHT_BIG_VEHICLES = 20;
        internal static int WEIGHT_PLAYER_IN_EMERGENCY_VEHICLE = 20;
        internal static int WEIGHT_NOT_HIGHWAY = -30;

        internal static void LoadSettings()
        {
            Game.LogTrivial("Loading plugin settings");
            var path = "Plugins/KeepRight.ini";
            var ini = new InitializationFile(path);
            ini.Create();

            DEBUG = ini.ReadBoolean("Debug", "DEBUG", false);
            if (DEBUG)
                Game.LogTrivial($"- DEBUG Features: enabled");

            CHECK_FREQUENCY = (float)ini.ReadDouble("Behavior", "CheckFrequency", 0.5f);
            Game.LogTrivial($"- Frequency: {CHECK_FREQUENCY}/sec");
            DISABLE_FOR_OTHER_EMERGENCY_VEHICLES = ini.ReadBoolean("Behavior", "DisableForOtherEmergencyVehicles", true);
            Game.LogTrivial($"- Disable for other emergency vehicles: {(DISABLE_FOR_OTHER_EMERGENCY_VEHICLES ? "Yes" : "No")}");
            ENABLE_BIG_VEHICLE_SIREN_ON = ini.ReadBoolean("Behavior", "EnableForBigVehicleWhenSirenOn", true);
            Game.LogTrivial($"- Enable for big vehicle when player siren is on: {(ENABLE_BIG_VEHICLE_SIREN_ON ? "Yes" : "No")}");

            WEIGHT_HORN = (float)ini.ReadDouble("Chances", "Horn", 2);
            Game.LogTrivial($"- Weight: Horn usage: {WEIGHT_HORN}");
            WEIGHT_BIG_VEHICLES = ini.ReadInt16("Chances", "BigVehicle", 20);
            Game.LogTrivial($"- Weight: Big vehicle: {WEIGHT_BIG_VEHICLES}");
            WEIGHT_PLAYER_IN_EMERGENCY_VEHICLE = ini.ReadInt16("Chances", "PlayerInEmergencyVehicle", 20);
            Game.LogTrivial($"- Weight: Player in emergency vehicle: {WEIGHT_PLAYER_IN_EMERGENCY_VEHICLE}");
            WEIGHT_NOT_HIGHWAY = ini.ReadInt16("Chances", "NotHighway", -30);
            Game.LogTrivial($"- Weight: Not on highway road: {WEIGHT_NOT_HIGHWAY}");

            Game.LogTrivial($"Plugin settings loaded.");
        }
    }
}
