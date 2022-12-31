//>> IceCalc

/*****************************************  
        Ice Calculator by Morphik.  
*****************************************/  
  
/*******************************  
             Settings  
*******************************/  
  
// LCD Name.  
string lcdName = "LCD Ice Calc";  
  
//Bar Graph Starting Character.                    
string start = "[";   
   
//Bar Graph Ending Character.                    
string end = "]";   
   
//Bar Graph Delimiter Character.                    
string bar = "I";   
   
//Bar Graph Empty Space Character.                    
string fill = "`";   
  
/*******************************   
          End Settings   
*******************************/  
   
List<IMyTerminalBlock> blocks = new List<IMyTerminalBlock>();       
List<IMyTerminalBlock> lcds = new List<IMyTerminalBlock>();       
List<IMyGasTank> gasTanks = new List<IMyGasTank>();     
     
public Program()     
{     
    Runtime.UpdateFrequency = UpdateFrequency.Update100;    
}  

int runAdvancer;

readonly string[] runStatus = new[] 
{ 
	"Program Running [|---]", 
	"Program Running [-|--]", 
	"Program Running [--|-]", 
	"Program Running [---|]", 
	"Program Running [--|-]", 
	"Program Running [-|--]" 
};
  
void Main()     
{     
    // Display All Information   
    IMyTextSurface pb = Me.GetSurface(0);
    pb.ContentType = ContentType.TEXT_AND_IMAGE;
    //pb.FontSize = 2;
    //pb.TextPadding = 5;
    //pb.Alignment = TextAlignment.CENTER;
    pb.WriteText("Ice to Hydrogen\n Calculator\n\n" + runStatus[runAdvancer]);
    runAdvancer = (runAdvancer + 1) % runStatus.Length;
    
    /************ Performance Debug ************  
    int counter = 1;            
    int maxSeconds = 30;             
    StringBuilder profile = new StringBuilder();            
    if (counter <= maxSeconds * 60)            
    {            
        double timeToRunCode = Runtime.LastRunTimeMs;            
            
        profile.Append(timeToRunCode.ToString("0.00")).Append("ms\n");            
        counter++;            
    }           
    Echo(profile.ToString());   
    *************************************************/ 
     
    if (!blocks.Any())    
    {   
        GridTerminalSystem.GetBlocksOfType<IMyTerminalBlock>(blocks, b => b.CubeGrid == Me.CubeGrid);      
        GridTerminalSystem.SearchBlocksOfName(lcdName, lcds, b => b.CubeGrid == Me.CubeGrid);     
        GridTerminalSystem.GetBlocksOfType<IMyGasTank>(gasTanks, b => b.CubeGrid == Me.CubeGrid);     
    }   
    string gridS = Me.CubeGrid.GridSizeEnum.ToString();     
     
    double iceAmount = 0;     
     
    if (gridS == "Large")     
    {     
        iceAmount = 277844.19;     
    }     
    else if (gridS == "Small")     
    {     
        iceAmount = 19995.74;     
    }    
     
    int hydTanksCount = 0;     
    double hydFull = 0;     
    double hydTankFill = 0;     
    double hydMath = 0;     
    double total = 0;  
   
    foreach (var block in blocks)      
    {           
        if (block.HasInventory)           
        {      
            List<MyInventoryItem> iceInventory = new List<MyInventoryItem>();
            block.GetInventory(0).GetItems(iceInventory);  
            string iceList = "";
            
            foreach (MyInventoryItem tempInv in iceInventory)
            {
                iceList = tempInv.Type.ToString();

                if (iceList.Contains("MyObjectBuilder_Ore/Ice"))           
                {      
                    //Echo(iceList + tempInv.Amount); 
                    total += (double)tempInv.Amount;
                } 
            }
        }            
    }   
     
    foreach (IMyGasTank tank in gasTanks)     
    {     
        MyResourceSinkComponent sink;     
        tank.Components.TryGet<MyResourceSinkComponent>(out sink);     
        var list = sink.AcceptedResources;     
        bool hasH2 = false;     
     
        foreach (var gas in list)     
        {     
            hasH2 = gas.SubtypeId.ToString() == "Hydrogen";     
            if (hasH2) { break; }     
        }     
        if (hasH2)     
        {     
            hydFull += tank.FilledRatio;     
            ++hydTanksCount;     
        }     
    }     
    hydFull /= hydTanksCount;     
    if (double.IsNaN(hydFull))  
    {  
        hydFull = 0;  
    }  
     
    hydTankFill = (iceAmount * hydTanksCount);     
    hydMath = (((iceAmount * hydTanksCount) * hydFull) - hydTankFill);     
      
      
    StringBuilder output = new StringBuilder();     
     
    output.Append(     
    "Hydrogen Tanks : " + hydTanksCount + "\n"     
    + "Ice needed to fill all tank(s) : " + hydTankFill.ToString("0.00") + "\n\n"     
    + "Hydrogen Tank Fill Level : " + "\n"     
    + barBuilder(hydFull) + "\n"  
    + "Ice to fill Tank(s) : " + Math.Abs(hydMath).ToString("0.00") + "\n"     
    + "Ice in Inventories : " + total.ToString("0.00") + "\n");     
    if ((Math.Abs(hydMath) - total) > 0)     
    {     
        output.Append("Missing Ice : " + (Math.Abs(hydMath) - total).ToString("0.00") + "\n");     
    }     
    else     
    {     
        output.Append("Missing Ice : None" + "\n");     
    }     
     
    Echo(output.ToString());     
     
    foreach (IMyTextPanel lcd in lcds)     
    {   
        lcd.ContentType = ContentType.TEXT_AND_IMAGE;
        //lcd.TextPadding = 0;
        //lcd.Alignment = TextAlignment.LEFT;
        //lcd.FontSize = 1;      
        lcd.WriteText(output.ToString());    
    }     
     
    blocks.Clear();     
    lcds.Clear();     
    gasTanks.Clear();     
}   
   
public string barBuilder(double num)   
{   
    double p = 0.0d;   
    int i = 0;   
    int l = 0;   
    StringBuilder barString = new StringBuilder();   
   
    p = num * 100;   
    barString.Append(start);   
    for (i = 0; i < (p / 2); i++)   
    {   
        barString.Append(bar);   
    }   
    l = 50 - i;   
    while (l > 0)   
    {   
        barString.Append(fill);   
        l--;   
    }   
    barString.Append(end);   
    barString.Append(" " + (p / 100).ToString("0.00" + " %") + "\n");   
    string barOutput = barString.ToString();   
    barString.Clear();   
    return barOutput;   
}