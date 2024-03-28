namespace Inventory
{
    public enum State
    {
        Reset,
        VerifySpawnPoints,
        ScanBatteries,
        ScanInventory,
        MoveItems,
        ScanAssemblerQueues,
        ProduceMissing,
        Report,
    }
}