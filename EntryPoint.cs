using Rage;
using Rage.Native;
using System;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

[assembly: Rage.Attributes.Plugin("KeepRight", Description = "Make the vehicles driving in front of you in the left lane on the highway change lanes", Author = "SSStuart", PrefersSingleInstance = true, SupportUrl = "https://ssstuart.net/discord")]

namespace KeepRight
{
    public static class EntryPoint
    {
        public static string pluginName = "KeepRight";
        public static string pluginVersion = Assembly.GetExecutingAssembly().GetName().Version.ToString();
        public static Localization l10n = new Localization();

        private static int checkDelayMs = 2000;
        private static int hornWeight = 0;
        private static int lastHornWeight = 0;

        public static void Main()
        {
            Game.LogTrivial($"{pluginName} plugin v{pluginVersion} has been loaded.");

            UpdateChecker.CheckForUpdates();

            Settings.LoadSettings();

            float checkFrequency = Settings.CHECK_KEY == Keys.None ? Settings.CHECK_FREQUENCY : 2;
            checkDelayMs = (int)(1 / checkFrequency) * 1000;

            if (Settings.CHECK_KEY != Keys.None)
            {
                GameFiber.StartNew(delegate
                {
                    while (true)
                    {
                        if (Game.IsKeyDown(Settings.CHECK_KEY) && Game.LocalPlayer.Character.Exists() && Game.LocalPlayer.Character.IsInAnyVehicle(false) && Game.LocalPlayer.Character.CurrentVehicle.Speed > 2)
                            CheckForVehicles();
                        GameFiber.Yield();
                    }
                });
            }
            else
            {
            if (Settings.CHECK_FREQUENCY == 0 || Settings.CHECK_FREQUENCY > 100)
            {
                Game.LogTrivial("Plugin stopped! Check frequency is " + (Settings.CHECK_FREQUENCY == 0 ? "too low" : "too high"));
                return;
            }

            GameFiber.StartNew(delegate
            {
                while (true)
                {
                    if (Game.LocalPlayer.Character.Exists() && Game.LocalPlayer.Character.IsInAnyVehicle(false) && Game.LocalPlayer.Character.CurrentVehicle.Speed > 2)
                    {
                        CheckForVehicles();
                    }
                    lastHornWeight = hornWeight;
                    hornWeight = 0;
                    GameFiber.Wait(checkDelayMs);
                }
            });

            GameFiber.StartNew(delegate
            {
                while (true)
                {
                    if (Game.LocalPlayer.Character.IsInAnyVehicle(false) && Game.LocalPlayer.IsPressingHorn)
                        hornWeight++;
                    GameFiber.Wait(100);
                }
            });
        }
        }

        private static void CheckForVehicles()
        {
            Vehicle[] drivingVehiclesInFront = World.GetEntities(GetEntitiesFlags.ConsiderGroundVehicles | GetEntitiesFlags.ExcludeEmptyVehicles | GetEntitiesFlags.ExcludePlayerVehicle | (Settings.ENABLE_FOR_OTHER_EMERGENCY_VEHICLES ? 0 : GetEntitiesFlags.ExcludeEmergencyVehicles)).Cast<Vehicle>().ToArray();
            drivingVehiclesInFront = drivingVehiclesInFront.Where((vehicle) => (Vector3.DistanceSquared(vehicle.Position, Game.LocalPlayer.Character.GetOffsetPositionFront(40)) < 40 * 40 && !vehicle.IsSirenOn && (vehicle.HasDriver && vehicle.Driver.Tasks.CurrentTaskStatus == TaskStatus.NoTask) && vehicle.Speed > 1 && vehicle.Speed <= Game.LocalPlayer.Character.Speed + 2 && Math.Abs(vehicle.Heading - Game.LocalPlayer.Character.Heading) < 40 && vehicle.GetPositionOffset(Game.LocalPlayer.Character.Position).X < 3 && vehicle.GetPositionOffset(Game.LocalPlayer.Character.Position).X > -5)).ToArray();
            
            foreach (Vehicle vehicle in drivingVehiclesInFront)
            {
                if (!vehicle.Exists() || !vehicle.HasDriver || vehicle.Metadata.isGoingRight ?? false)
                    continue;

                Vector3 nodeCheckPos = vehicle.GetOffsetPositionFront(10);

                NativeFunction.Natives.GET_CLOSEST_VEHICLE_NODE_WITH_HEADING(
                    nodeCheckPos.X, nodeCheckPos.Y, nodeCheckPos.Z,
                    out Vector3 nodePos, out float nodeHeading, 0, 3.0f, 0f);

                vehicle.Metadata.isGoingRight = true;

                GameFiber.StartNew(delegate
                {
                    // Random delay to avoid triggering the lane change at a fixed frequency
                    GameFiber.Sleep(MathHelper.GetRandomInteger(0, checkDelayMs));
                    if (!vehicle.Exists() || !vehicle.HasDriver)
                        return;

                    NativeFunction.Natives.GET_CLOSEST_VEHICLE_NODE_WITH_HEADING(
                       nodeCheckPos.X, nodeCheckPos.Y, nodeCheckPos.Z,
                       out nodePos, out float _, 0, 3.0f, 0f);
                    NativeFunction.Natives.GET_ROAD_BOUNDARY_USING_HEADING(
                        nodeCheckPos.X, nodeCheckPos.Y, nodeCheckPos.Z,
                        vehicle.Heading, out Vector3 roadBoundPos);
                    NativeFunction.Natives.GET_VEHICLE_NODE_PROPERTIES(
                        nodePos.X, nodePos.Y, nodePos.Z,
                        out int _, out int flags);

                    bool isOnHighway = (flags & (1 << 6)) != 0;
                    bool isOnLeftSideOfRoad = vehicle.GetPositionOffset(nodePos).X > 0;
                    bool doesLaneToRightExist = Math.Sqrt(Vector3.DistanceSquared(nodeCheckPos, roadBoundPos) - Vector3.DistanceSquared(nodeCheckPos, nodePos)) > 5;
                    bool isRightLaneOccupied = World.TraceBox(vehicle.GetOffsetPosition(new Vector3(4, 0, 0)), vehicle.Orientation, new Vector3(2, vehicle.Length + vehicle.Speed, 3), TraceFlags.IntersectVehicles, vehicle).Hit;
                    if (isRightLaneOccupied && Settings.ACCELERATE_IF_CANT_CHANGE_LANES)
                    {
                        vehicle.Metadata.KR_timeout = Game.GameTime + 5000;
                        vehicle.Driver.Tasks.PerformDrivingManeuver(VehicleManeuver.GoForwardStraightHalfAcceleration).WaitForCompletion(100); // Seems needed for CruiseWithVehicle to take effect
                        vehicle.Driver.Tasks.CruiseWithVehicle(vehicle.Speed + 3f).WaitForCompletion(5000);
                        vehicle.Driver.Tasks.Clear();
                    }

                    bool isPlayerSirenOn = Game.LocalPlayer.Character.CurrentVehicle.IsSirenOn;

                    float mergeChance = (60 - vehicle.DistanceTo2D(Game.LocalPlayer.Character.Position)
                        + (!isOnHighway ? Settings.WEIGHT_NOT_HIGHWAY : 0)
                        + (lastHornWeight * Settings.WEIGHT_HORN)
                        + (vehicle.IsBig ? Settings.WEIGHT_BIG_VEHICLES : 0)
                        + (Game.LocalPlayer.Character.CurrentVehicle.Model.IsEmergencyVehicle ? Settings.WEIGHT_PLAYER_IN_EMERGENCY_VEHICLE : 0)
                        + (isPlayerSirenOn ? 50 : 0)
                        + (Settings.CHECK_KEY != Keys.None ? Settings.WEIGHT_KEY : 0)) / 100;

                    if ((!isPlayerSirenOn || (Settings.ENABLE_BIG_VEHICLE_SIREN_ON && vehicle.IsBig)) && (isOnLeftSideOfRoad || doesLaneToRightExist) && !isRightLaneOccupied && MathHelper.GetRandomSingle(0, 1) < mergeChance)
                    {
                        float taskSpeed = Math.Max(vehicle.Speed, 5);
                        Vector3 targetRoadPosition = vehicle.GetOffsetPosition(new Vector3(5, 3 * taskSpeed, 0));
                        bool isTargetOnRoad = false;
                        int retryNb = 0;
                        do
                        {
                            targetRoadPosition = vehicle.GetOffsetPosition(new Vector3(5, (3 - retryNb) * taskSpeed, 0));
                            isTargetOnRoad = NativeFunction.Natives.IS_POINT_ON_ROAD<bool>(targetRoadPosition.X, targetRoadPosition.Y, targetRoadPosition.Z, vehicle);
                            retryNb++;
                        }
                        while (!isTargetOnRoad && retryNb < 3);
                        if (!isTargetOnRoad)
                        {
                            vehicle.Metadata.isGoingRight = false;
                            return;
                        }
                        NativeFunction.Natives.GET_CLOSEST_VEHICLE_NODE_WITH_HEADING(
                           targetRoadPosition.X, targetRoadPosition.Y, targetRoadPosition.Z,
                           out Vector3 targetNodePos, out float _, 0, 3.0f, 0f);

                        float targetXOffset = vehicle.GetPositionOffset(nodePos).X - vehicle.GetPositionOffset(targetNodePos).X;
                        targetRoadPosition.X -= MathHelper.Clamp(targetXOffset, -4, 4);

                        Game.LogTrivial($"Starting lane change for {vehicle.Model.Name}. Current heading: {vehicle.Heading} - Target: {targetRoadPosition}");
                        Blip mergingVehicleBlip = null;
                        GameFiber drawLine = null;
                        if (Settings.DEBUG)
                        {
                            Game.DisplayNotification($"Starting merge for {vehicle.Model.Name}");
                            mergingVehicleBlip = new Blip(vehicle)
                            {
                                Sprite = BlipSprite.CarWash,
                                Alpha = 0.5f
                            };
                            GameFiber drawline = GameFiber.StartNew(delegate {
                                uint endTime = Game.GameTime + 5000;
                                while (Game.GameTime < endTime && vehicle.Exists())
                                {
                                    NativeFunction.Natives.DRAW_LINE(targetRoadPosition.X, targetRoadPosition.Y, targetRoadPosition.Z + 0.5f, vehicle.Position.X, vehicle.Position.Y, vehicle.Position.Z, 50, 50, 255, 200);
                                    GameFiber.Yield();
                                }
                            });
                        }
                        vehicle.Driver.Tasks.DriveToPosition(targetRoadPosition, taskSpeed, VehicleDrivingFlags.DriveAroundObjects | VehicleDrivingFlags.DriveAroundPeds | VehicleDrivingFlags.DriveAroundVehicles | VehicleDrivingFlags.IgnorePathFinding | VehicleDrivingFlags.FollowTraffic, taskSpeed / 2).WaitForCompletion(10000);
                        if (vehicle.Exists())
                        {
                            if (vehicle.HasDriver)
                                vehicle.Driver.Tasks.Clear();
                            vehicle.Metadata.isGoingRight = false;
                            if (Settings.DEBUG && mergingVehicleBlip.Exists())
                                mergingVehicleBlip.Delete();
                            if (Settings.DEBUG && drawLine != null)
                                drawLine.Abort();
                        }
                    } else
                        vehicle.Metadata.isGoingRight = false;
                });

                GameFiber.Yield();
            }
        }

        private static void OnUnload(bool variable)
        {
            Game.LogTrivial("Unloading...");

            Game.LogTrivial("Unloaded");
        }
    }
}
