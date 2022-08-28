using MathUnit;
using TrackPlanner.Data.Serialization;

namespace TrackPlanner.Data
{
    public sealed class UserTurnerPreferences
    {
        public Angle StraigtLineAngleLimit { get; set; }
        public Length InitSnapProximityLimit { get; set; }
        public Length FinalSnapProximityLimit { get; set; }
        public Angle AltAngleDifferenceLowLimit { get; set; }
        public Angle AltAngleDifferenceHighLimit { get; set; }
        public Angle AltMinorAngleSlack { get; set; }
        public Length CyclewayExitDistanceLimit { get; set; }
        public Angle CyclewayExitAngleLimit { get; set; }
        public Length CyclewayRoadParallelLength { get; set; }
        public Length TurnArmLength { get; set; }
        public Length MinimalCrossIntersection { get; set; }
        public Angle CrossIntersectionAngleSeparation { get; set; }

        public UserTurnerPreferences()
        {
            StraigtLineAngleLimit = Angle.FromDegrees(150);
            AltAngleDifferenceLowLimit = Angle.FromDegrees(50);

            InitSnapProximityLimit = Length.FromMeters(25);
            FinalSnapProximityLimit = Length.FromKilometers(10);
            AltAngleDifferenceHighLimit = Angle.FromDegrees(30);
            AltMinorAngleSlack = Angle.FromDegrees(10);
            CyclewayExitDistanceLimit = Length.FromMeters(10);
            CyclewayExitAngleLimit = Angle.FromDegrees(165);
            CyclewayRoadParallelLength = Length.FromMeters(30);
            TurnArmLength = Length.FromMeters(20);
            MinimalCrossIntersection = Length.FromMeters(10);
            CrossIntersectionAngleSeparation = Angle.FromDegrees(45); // +  -- if any of the arms are closer, we won't treat it as proper cross intersection (it is too squeezed)
        }

        public override string ToString()
        {
            return new ProxySerializer().Serialize(this);
        }
    }

   

}