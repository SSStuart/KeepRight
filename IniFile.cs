using Rage;

namespace KeepRight
{
    internal static class Settings
    {
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

            WEIGHT_HORN = (float)ini.ReadDouble("Chances", "Horn", 2);
            Game.LogTrivial($"- Weight: Horn usage: {WEIGHT_HORN}");
            WEIGHT_BIG_VEHICLES = ini.ReadInt16("Chances", "BigVehicle", 20);
            Game.LogTrivial($"- Weight: Big vehcile: {WEIGHT_BIG_VEHICLES}");
            WEIGHT_PLAYER_IN_EMERGENCY_VEHICLE = ini.ReadInt16("Chances", "PlayerInEmergencyVehicle", 20);
            Game.LogTrivial($"- Weight: Player in emergency vehicle: {WEIGHT_PLAYER_IN_EMERGENCY_VEHICLE}");
            WEIGHT_NOT_HIGHWAY = ini.ReadInt16("Chances", "NotHighway", -30);
            Game.LogTrivial($"- Weight: Not on highway road: {WEIGHT_NOT_HIGHWAY}");

            Game.LogTrivial($"Plugin settings loaded.");
        }
    }
}
