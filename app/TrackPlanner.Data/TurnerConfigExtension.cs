using MathUnit;

namespace TrackPlanner.Data
{
    public static class TurnerConfigExtension
    {
        public static Angle GetAltAngleDifferenceLimit(this UserTurnerPreferences preferences, Angle altAngle)
        {
            // the more straighter is our track, the more straigher the alternate has to be as well, to force turn-noficication
            double scaling = (Angle.PI - altAngle) / (Angle.PI - preferences.StraigtLineAngleLimit);
            return preferences.AltAngleDifferenceHighLimit + (preferences.AltAngleDifferenceLowLimit - preferences.AltAngleDifferenceHighLimit) * scaling;
        }
    }

}
