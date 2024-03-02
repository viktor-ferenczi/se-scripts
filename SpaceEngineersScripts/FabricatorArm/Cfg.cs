namespace SpaceEngineersScripts.FabricatorArm
{
    public static class Cfg
    {
        // Name of the projector to receive the projection information from via MGP's PB API (required)
        public const string ProjectorName = "Shipyard Projector";

        // Name of the block group containing the first mechanical bases of each arm (required)
        public const string WelderArmsGroupName = "Fabricator Arms";

        // Name of the block group containing LCD panels to show completion statistics and debug information (optional)
        // Names should contains: Timer, Details, Status, Log
        public const string TextPanelsGroupName = "Shipyard Text Panels";
    }
}