namespace RobotArm
{
    public class Config : BaseConfig
    {
        public static Config Instance;
        
        public string ProjectorName => (string)this["ProjectorName"];
        public string ProjectorRotorName => (string)this["ProjectorRotorName"];
        public string WelderArmsGroupName => (string)this["WelderArmsGroupName"];
        public string TextPanelsGroupName => (string)this["TextPanelsGroupName"];
        public double DirectionCostWeight => (double)this["DirectionCostWeight"];
        public double RollCostWeight => (double)this["RollCostWeight"];
        public double ActivationRegularization => (double)this["ActivationRegularization"];
        public double MaxWeldingDistanceLargeWelder => (double)this["MaxWeldingDistanceLargeWelder"];
        public double MaxWeldingDistanceSmallWelder => (double)this["MaxWeldingDistanceSmallWelder"];
        public int OptimizationPasses => (int)this["OptimizationPasses"];
        public int MaxRetractionTimeAfterCollision => (int)this["MaxRetractionTimeAfterCollision"];
        public int MaxRetractionTimeAfterUnreachable => (int)this["MaxRetractionTimeAfterUnreachable"];
        public double MovingCostIncreaseLimit => (double)this["MovingCostIncreaseLimit"];
        public int MovingTimeout => (int)this["MovingTimeout"];
        public int WeldingTimeout => (int)this["WeldingTimeout"];
        public int ResetArmAfterFailedWeldingAttempts => (int)this["ResetArmAfterFailedWeldingAttempts"];
        public double MinActivationStepPiston => (double)this["MinActivationStepPiston"];
        public double MinActivationStepRotor => (double)this["MinActivationStepRotor"];
        public double MinActivationStepHinge => (double)this["MinActivationStepHinge"];
        public int MaxLargeBlocksToWeld => (int)this["MaxLargeBlocksToWeld"];
        public int MaxSmallBlocksToWeld => (int)this["MaxSmallBlocksToWeld"];
        
        protected override void AddOptions()
        {
            Descriptions["ProjectorName"] = "Name of the projector to receive the projection\ninformation from via MGP's PB API (required)";
            Defaults["ProjectorName"] = "Shipyard Projector";

            Descriptions["ProjectorRotorName"] = "Name of the rotor rotating the projector (optional),\nthe program makes sure to reverse this rotor\nif it becomes stuck due to an arm in the way";
            Defaults["ProjectorRotorName"] = "Shipyard Projector Rotor";

            Descriptions["WelderArmsGroupName"] = "Name of the block group containing the first\nmechanical bases of each arm (required)";
            Defaults["WelderArmsGroupName"] = "Welder Arms";

            Descriptions["TextPanelsGroupName"] = "Name of the block group containing LCD panels\nto show completion statistics and\ndebug information (optional).\nNames should contain: Timer, Details, Status, Log";
            Defaults["TextPanelsGroupName"] = "Shipyard Text Panels";

            Descriptions["DirectionCostWeight"] = "Weight of the direction component of the\noptimized effector pose in the cost,\nhigher value prefers more precise effector direction.\nSet to 1.0 to turn the welder arm towards\nthe preview grid's center.";
            Defaults["DirectionCostWeight"] = 1.0; 

            Descriptions["RollCostWeight"] = "Weight of the roll component of the optimized effector pose,\nhigher value prefers more precise roll control.\nWelders don't care about roll,\nso set this to zero (no need to optimize for roll).";
            Defaults["RollCostWeight"] = 0.0; 

            Descriptions["ActivationRegularization"] = "L2 regularization of mechanical base activations,\nhigher value prefers simpler arm poses\ncloser to the initial activations";
            Defaults["ActivationRegularization"] = 2.0;

            Descriptions["MaxWeldingDistanceLargeWelder"] = "Maximum distance from the large welder's effector\ntip to weld blocks, it applies to block intersection,\nnot to the distance of their center";
            Defaults["MaxWeldingDistanceLargeWelder"] = 2.26; // [m]
            
            Descriptions["MaxWeldingDistanceSmallWelder"] = "Maximum distance from the small welser's effector\ntip to weld blocks, it applies to block intersection,\nnot to the distance of their center";
            Defaults["MaxWeldingDistanceSmallWelder"] = 1.3; // [m]

            Descriptions["OptimizationPasses"] = "Maximum number of full forward-backward optimization\npasses along the arm segments each tick";
            Defaults["OptimizationPasses"] = 1;

            Descriptions["MaxRetractionTimeAfterCollision"] = "Maximum time to retract the arm after a collision\non moving the arm to the target block or during welding";
            Defaults["MaxRetractionTimeAfterCollision"] = 3; // [Ticks] (1/6 seconds, due to Update10)

            Descriptions["MaxRetractionTimeAfterUnreachable"] = "Maximum time to retract the arm after a block\nproved to be unreachable after the arm tried to reach it";
            Defaults["MaxRetractionTimeAfterUnreachable"] = 6; // [Ticks] (1/6 seconds, due to Update10)

            Descriptions["MovingCostIncreaseLimit"] = "If the arm moves the wrong direction then\nconsider the target as unreachable";
            Defaults["MovingCostIncreaseLimit"] = 50.0;

            Descriptions["MovingTimeout"] = "Timeout moving the arm near the target block,\ncounted until welding range";
            Defaults["MovingTimeout"] = 20; // [Ticks] (1/6 seconds, due to Update10)

            Descriptions["WeldingTimeout"] = "Timeout for welding a block";
            Defaults["WeldingTimeout"] = 6; // [Ticks] (1/6 seconds, due to Update10)

            Descriptions["ResetArmAfterFailedWeldingAttempts"] = "Resets the arm after this many subsequent\nfailed welding attempts";
            Defaults["ResetArmAfterFailedWeldingAttempts"] = 5;

            Descriptions["MinActivationStepPiston"] = "Minimum meaningful activation steps during\noptimization for pistons [m]"; 
            Defaults["MinActivationStepPiston"] = 0.001;
            
            Descriptions["MinActivationStepRotor"] = "Minimum meaningful activation steps during\noptimization for rotors [rad]"; 
            Defaults["MinActivationStepRotor"] = 0.001;
            
            Descriptions["MinActivationStepHinge"] = "Minimum meaningful activation steps during\noptimization for hinges [rad]"; 
            Defaults["MinActivationStepHinge"] = 0.001;

            Descriptions["MaxLargeBlocksToWeld"] = "Maximum number of blocks to weld\nat the same time on large grid";
            Defaults["MaxLargeBlocksToWeld"] = 1;
            
            Descriptions["MaxSmallBlocksToWeld"] = "Maximum number of blocks to weld\nat the same time on small grid";
            Defaults["MaxSmallBlocksToWeld"] = 125;
        }
    }
}