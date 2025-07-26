namespace RobotArm
{
    public class BlockConfig: BaseConfig
    {
        public double VelocityMultiplier => (double)this["VelocityMultiplier"];
        
        protected override void AddOptions()
        {
            Descriptions["VelocityMultiplier"] = "Velocity multiplier";
            Defaults["VelocityMultiplier"] = 1.0;
        }
    }
}