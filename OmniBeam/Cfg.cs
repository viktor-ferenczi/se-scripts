namespace OmniBeam
{
    //!!
    /* OmniBeam Arm Controller: https://github.com/viktor-ferenczi/se-scripts */
    public static class Cfg
    {
        // Name of the projector to receive the projection information from via MGP's PB API (required)
        public const string ProjectorName = "Shipyard Projector";

        // Name of the block group containing the azimuth rotor bases of each arm (required)
        public const string WelderArmsGroupName = "OmniBeam Arms";

        // Name of the block group containing LCD panels to show completion statistics and debug information (optional)
        // Names should contain: Timer, Details, Status, Log
        public const string TextPanelsGroupName = "Shipyard Text Panels";

        // Targeting and welding deadline in seconds, once it expires the welder will reset and finds a random target
        public const int WeldingDeadline = 10;  // seconds

        // Randomization of welding laser target position, it helps to weld blocks which don't cover their center
        public const double PositionRandomization = 1.0; // Zero turns it OFF, 1 means the full target cube
    }
}