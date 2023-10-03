using NCalc;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace prgToXML
{
    public sealed partial class MainPage : Page
    {
        public MainPage()
        {
            this.InitializeComponent();
        }

        private async void Button_Click(object sender, RoutedEventArgs e)
        {
            FolderPicker folderPicker = new FolderPicker();
            folderPicker.SuggestedStartLocation = PickerLocationId.ComputerFolder;
            folderPicker.FileTypeFilter.Add(".prg"); // Select only PRG files


            var folder = await folderPicker.PickSingleFolderAsync();

            TextBoxOutput.Text = "";
            TextBlock_Dir.Text = "Dizin: ";

            if (folder != null)
            {
                // Print out Dir
                TextBlock_Dir.Text += folder.Path;

                // Find all PRG files in the selected directory
                IReadOnlyList<StorageFile> prgFiles = await folder.GetFilesAsync();
                List<string> fileContents = new List<string>();

                foreach (StorageFile file in prgFiles)
                {
                    try
                    {
                        string fileContent = await FileIO.ReadTextAsync(file);
                        fileContents.Add(fileContent);

                        // You can start processing the content of each file here.
                        // For example, you can analyze the commands in the file.
                    }
                    catch (Exception ex)
                    {
                        var dialog = new MessageDialog("File read error (" + file.Name + "): " + ex.Message);
                        await dialog.ShowAsync();
                    }
                }

                // After processing the file contents, you can perform the necessary conversion tasks.
                await ConvertAndSave(folder, fileContents, prgFiles);
            }
        }

        private async Task ConvertAndSave(StorageFolder folder, List<string> fileContents, IReadOnlyList<StorageFile> prgFiles)
        {
            for (int i = 0; i < fileContents.Count; i++)
            {
                string fileContent = fileContents[i];
                StorageFile prgFile = prgFiles[i];
                string fileName = prgFile.DisplayName;

                // Get Tool Data
                string frzToolName = textBox1.Text;
                string frzToolDia = textBox2.Text;

                // Create an empty XML document initially
                XmlDocument xmlDoc = new XmlDocument();

                // Create KDTPanelFormat element
                XmlElement kdtPanelFormatElement = XmlHelper.AddElement(xmlDoc, xmlDoc, "KDTPanelFormat");

                // Conversion tasks are performed here.
                // The content of each file is found in the "fileContent" variable.
                string xmlData = ConvertPRGtoXML(fileContent, fileName, xmlDoc, kdtPanelFormatElement, frzToolName, frzToolDia);


                // Add the file name to a TextBox
                TextBoxOutput.Text += $"{fileName}\n";

                if (!string.IsNullOrEmpty(xmlData))
                {
                    // You can retrieve the name of the converted PRG file.

                    await SaveXMLFile(fileName, xmlData, folder); // Save the MPR file
                }
            }
        }

        private string ConvertPRGtoXML(string prgIcerik, string fileName, XmlDocument xmlDoc, XmlElement kdtPanelFormatElement, string frzToolName, string frzToolDia)
        {
            List<string> mprLines = new List<string>();
            Dictionary<string, double> vars = new Dictionary<string, double>();
            Dictionary<string, string> frzElementsList = new Dictionary<string, string>();

            Dictionary<string, string> frzToolsList = new Dictionary<string, string>
            {
                { "T2", "15" },
                { "T3", "4" },
                { "T5", "5" },
                { "T6", "5" },
                { "T9", "8" },
                { "T35", "8" },
                { "T36", "6" },
                { "T37", "8" },
                { "T38", "6" }
            };

            frzElementsList["x"] = "";
            frzElementsList["y"] = "";
            frzElementsList["z"] = "";

            string[] lines = prgIcerik.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            bool inG100Block = false;   // PROGRAMMING OF TOP HOLES
            bool inG101Block = false;   // PROGRAMMING OF VERTICAL HOLES
            bool inG172Block = false;   // SELECTION OF THE START POINT IN FIXED CYCLES +TOOL, ROTATION SPEED, ENTRY SPEED, ROUTING BIT INCLINATIONS 
            //bool inG173Block = false;   // INTERPOLATION ORIGIN INDIPENDENT FROM WORKING FACE
            bool inG182Block = false;   // HORIZONTAL HOLES IN WORKPIECE LEFT FACE
            bool inG183Block = false;   // HORIZONTAL HOLES IN WORKPIECE RIGHT FACE
            //bool inG184Block = false;   // HORIZONTAL HOLES IN WORKPIECE REAR FACE
            //bool inG185Block = false;   // HORIZONTAL HOLES IN WORKPIECE FRONT FACE

            bool inWhileLoop = false;   // HORIZONTAL HOLES IN WORKPIECE FRONT FACE
            int whileLoopIndexStart = 0;
            int whileLoopIndexEnd = 0;
            string whileCond = "false";

            bool firstPoint = false;   // INTERPOLATION ORIGIN INDIPENDENT FROM WORKING FACE
            int frzCnt = 0;
            int frzOpCnt = 0;

            string w, l, th, lf2, wf2, thf2;
            w = "";
            th = "";
            l = "";


            // Create PANEL element
            XmlElement panelElement = XmlHelper.AddElement(xmlDoc, kdtPanelFormatElement, "PANEL");

            // Start processing the PRG content
            XmlNode frzElementsVertexes = panelElement; // C# doesnt want to have  null pointer, no meaning to assing this node
            XmlNode frzCadElement = panelElement; // C# doesnt want to have  null pointer, no meaning to assing this node

            string toolCorrention = "2"; // 1: Right 2: Left

            for (int i = 0; i < lines.Length; i++)
            {

                string line = lines[i]; // delete the row

                string trimmedLine = line.Trim();

                if ((trimmedLine.StartsWith("G100") || trimmedLine.StartsWith("G172") || trimmedLine.StartsWith("G173") || trimmedLine.StartsWith("G182") || trimmedLine.StartsWith("G183") || trimmedLine.StartsWith("%")) && inG172Block)
                {
                    inG172Block = false;
                }

                if (trimmedLine.StartsWith("G172"))
                {
                    if (!inG172Block) inG172Block = true;
                    if (!firstPoint) firstPoint = true;

                    frzOpCnt = 0;
                    frzCnt++;

                    string z = "";

                    string[] parts = trimmedLine.Split(' ');
                    foreach (string part in parts)
                    {
                        if (part == "G172" || part == "X" || part == "Y") continue;
                        else if (part.StartsWith("Z")) z = ProcessCoordinatePart(part, vars);
                    }

                    //mprLines.Add("]" + frzCnt);

                    // Create CAD element
                    XmlElement cadElement3 = XmlHelper.AddElement(xmlDoc, kdtPanelFormatElement, "CAD");

                    XmlHelper.AddElement(xmlDoc, cadElement3, "TypeNo", "7");
                    XmlHelper.AddElement(xmlDoc, cadElement3, "TypeName", "Path");
                    XmlHelper.AddElement(xmlDoc, cadElement3, "Width", frzToolDia);
                    XmlHelper.AddElement(xmlDoc, cadElement3, "ToolName", frzToolName);

                    XmlHelper.AddElement(xmlDoc, cadElement3, "Depth", z);
                    //XmlHelper.AddElement(xmlDoc, cadElement3, "Correction", toolCorrention);
                    XmlHelper.AddElement(xmlDoc, cadElement3, "Close", "0");
                    XmlHelper.AddElement(xmlDoc, cadElement3, "Empty", "0");
                    XmlHelper.AddElement(xmlDoc, cadElement3, "Enable", "1");

                    // Create Vertexes element
                    XmlElement vertexesElement = XmlHelper.AddElement(xmlDoc, cadElement3, "Vertexes");
                    frzElementsVertexes = vertexesElement;
                    frzCadElement = cadElement3;
                }

                if (trimmedLine.StartsWith("|4 "))
                {
                    w = trimmedLine.Split(new string[] { "|4 " }, StringSplitOptions.None)[1];
                }
                else if (trimmedLine.StartsWith("|5 "))
                {
                    th = trimmedLine.Split(new string[] { "|5 " }, StringSplitOptions.None)[1];
                }
                else if (trimmedLine.StartsWith("|6 "))
                {
                    l = trimmedLine.Split(new string[] { "|6 " }, StringSplitOptions.None)[1];


                    // convert data

                    wf2 = "";
                    lf2 = "";
                    thf2 = "";

                    if (double.TryParse(w, out double val1)) wf2 = val1.ToString("F2");
                    if (double.TryParse(l, out double val2)) lf2 = val2.ToString("F2");
                    if (double.TryParse(th, out double val3)) thf2 = val3.ToString("F2");

                    // Add other elements to PANEL
                    XmlHelper.AddElement(xmlDoc, panelElement, "CoordinateSystem", "3");
                    XmlHelper.AddElement(xmlDoc, panelElement, "PanelLength", w);
                    XmlHelper.AddElement(xmlDoc, panelElement, "PanelWidth", l);
                    XmlHelper.AddElement(xmlDoc, panelElement, "PanelThickness", th);
                    XmlHelper.AddElement(xmlDoc, panelElement, "PanelName", fileName);
                    XmlHelper.AddElement(xmlDoc, panelElement, "PanelMaterial", "[DEFAULT]");
                    XmlHelper.AddElement(xmlDoc, panelElement, "PanelTexture", "0");
                    XmlHelper.AddElement(xmlDoc, panelElement, "PanelQuantity", "1");
                    XmlHelper.AddElement(xmlDoc, panelElement, "Inch", "0");

                    // Create Params element and add it to PANEL element
                    XmlElement paramsElement = XmlHelper.AddElement(xmlDoc, panelElement, "Params");

                    //Create and append Param elements
                    XmlHelper.AddParam(xmlDoc, paramsElement, wf2, "Lenght", "x");
                    XmlHelper.AddParam(xmlDoc, paramsElement, lf2, "Width", "y");
                    XmlHelper.AddParam(xmlDoc, paramsElement, thf2, "Thickness", "th");

                }
                else if (trimmedLine.StartsWith("#WHILE"))  // LOOP
                {
                    if (!inWhileLoop)
                    {
                        inWhileLoop = true;
                        whileLoopIndexStart = i - 1;
                    }
                    whileLoopIndexEnd = 0;

                    for (int j = i; j < lines.Length; j++) if (lines[j].StartsWith("#WEND")) whileLoopIndexEnd = j;

                    if (whileLoopIndexEnd == 0) // Error While Loop without WEND
                    {
                        ShowErrorMessage("The WHILE loop has not ended with WEND.");
                        break;
                    }

                    string[] parcalar = trimmedLine.Split(' ');
                    whileCond = parcalar[1].Trim();
                    whileCond = ReplaceVariables(vars, whileCond);
                    whileCond = Calculate(whileCond);
                }

                else if (trimmedLine.StartsWith("#WEND"))  // LOOP
                {

                    if (!CheckBox01.IsChecked.HasValue || CheckBox01.IsChecked.Value == false)
                    {
                        // CheckBox is not checked.
                        whileCond = "False";
                    }
                    if (whileCond == "True") i = whileLoopIndexStart;
                    else inWhileLoop = false;
                }

                else if (trimmedLine.StartsWith("#") && !trimmedLine.StartsWith("#WEND"))
                {
                    string[] parcalar = trimmedLine.Split('=');
                    string varName = parcalar[0].Trim();
                    string str = parcalar[1].Trim();

                    if (str == "DX") str = w;
                    if (str == "DY") str = l;


                    str = ReplaceVariables(vars, str);
                    if (Regex.IsMatch(str, @"[\+\-\*/]")) str = Calculate(str);



                    vars[varName] = Convert.ToDouble(str);
                }

                else if (trimmedLine.StartsWith("G101"))
                {

                    if (!inG101Block) inG101Block = true;

                    string x, y, z;
                    x = "";
                    y = "";
                    z = "";
                    //xf2 = "";
                    //yf2 = "";
                    //zf2 = "";

                    string[] parts = trimmedLine.Split(' ');
                    foreach (string part in parts)
                    {
                        if (part == "G101") continue;
                        else if (part.StartsWith("X")) x = ProcessCoordinatePart(part, vars);
                        else if (part.StartsWith("Y")) y = ProcessCoordinatePart(part, vars);
                        else if (part.StartsWith("Z")) z = ProcessCoordinatePart(part, vars);
                    }

                    // Eğer bir önceki frezeleme ile aynı ise işlem yapma
                    if (frzElementsList["x"] == x && frzElementsList["y"] == y && frzElementsList["z"] == z) continue;

                    frzElementsList["x"] = x;
                    frzElementsList["y"] = y;
                    frzElementsList["z"] = z;

                    //frzOpCnt++;


                    if (firstPoint)
                    {
                        //mprLines.Add("KP");

                        // Create Point element
                        XmlElement pointElement = XmlHelper.AddElement(xmlDoc, frzElementsVertexes, "Point");
                        XmlHelper.AddElement(xmlDoc, pointElement, "X1", x);
                        XmlHelper.AddElement(xmlDoc, pointElement, "Y1", y);
                        XmlHelper.AddElement(xmlDoc, pointElement, "Z1", "0");

                        firstPoint = false;
                    }
                    else
                    {
                        // Create Line elements (you can add more as needed)
                        XmlElement lineElement1 = XmlHelper.AddElement(xmlDoc, frzElementsVertexes, "Line");
                        XmlHelper.AddElement(xmlDoc, lineElement1, "X1", x);
                        XmlHelper.AddElement(xmlDoc, lineElement1, "Y1", y);
                        XmlHelper.AddElement(xmlDoc, lineElement1, "Z1", "0");
                    }


                }
                else if (trimmedLine.StartsWith("G102"))
                {

                    if (!inG101Block) inG101Block = true;

                    string x, y, z, r;
                    x = "";
                    y = "";
                    z = "";
                    r = "";


                    string[] parts = trimmedLine.Split(' ');
                    foreach (string part in parts)
                    {
                        if (part == "G102") continue;
                        else if (part.StartsWith("X")) x = ProcessCoordinatePart(part, vars);
                        else if (part.StartsWith("Y")) y = ProcessCoordinatePart(part, vars);
                        else if (part.StartsWith("Z")) z = ProcessCoordinatePart(part, vars);
                        else if (part.StartsWith("R")) r = ProcessCoordinatePart(part, vars);
                    }

                    // Eğer bir önceki frezeleme ile aynı ise işlem yapma
                    //if (frzElementsList["x"] == x && frzElementsList["y"] == y && frzElementsList["z"] == z) continue;

                    //frzElementsList["x"] = x;
                    //frzElementsList["y"] = y;
                    //frzElementsList["z"] = z;

                    //mprLines.Add("$E" + frzOpCnt.ToString());
                    //frzOpCnt++;

                    // Create Arc element


                    XmlElement arcElement2 = XmlHelper.AddElement(xmlDoc, frzElementsVertexes, "Arc");
                    XmlHelper.AddElement(xmlDoc, arcElement2, "X1", x);
                    XmlHelper.AddElement(xmlDoc, arcElement2, "Y1", y);
                    XmlHelper.AddElement(xmlDoc, arcElement2, "Z1", "0");
                    XmlHelper.AddElement(xmlDoc, arcElement2, "Radius", r);
                    XmlHelper.AddElement(xmlDoc, arcElement2, "Direction", "1");


                }
                else if (trimmedLine.StartsWith("G42"))
                {
                    // The milling cutter is performing an operation on the Right edge.
                    toolCorrention = "1";
                    if (!XmlHelper.ElementExists(frzCadElement, "Correction")) XmlHelper.AddElement(xmlDoc, frzCadElement, "Correction", toolCorrention);
                }
                else if (trimmedLine.StartsWith("G41"))
                {
                    // The milling cutter is performing an operation on the left edge.
                    toolCorrention = "2";
                    if (!XmlHelper.ElementExists(frzCadElement, "Correction")) XmlHelper.AddElement(xmlDoc, frzCadElement, "Correction", toolCorrention);
                }
                else if (trimmedLine.StartsWith("G40"))
                {
                    // The milling cutter is performing an operation from the center.
                    toolCorrention = "0";
                    if (!XmlHelper.ElementExists(frzCadElement, "Correction")) XmlHelper.AddElement(xmlDoc, frzCadElement, "Correction", toolCorrention);
                }
                else if (trimmedLine.StartsWith("G100"))    // Vertical Hole
                {
                    if (!inG100Block) inG101Block = true;

                    string x, y, z, toolDia;
                    x = "";
                    y = "";
                    z = "";
                    toolDia = "";

                    string[] parts = trimmedLine.Split(' ');
                    foreach (string part in parts)
                    {
                        if (part == "G100") continue;
                        else if (part.StartsWith("X")) x = ProcessCoordinatePart(part, vars);
                        else if (part.StartsWith("Y")) y = ProcessCoordinatePart(part, vars);
                        else if (part.StartsWith("Z")) z = ProcessCoordinatePart(part, vars);
                        else if (part.StartsWith("T"))
                        {
                            if (frzToolsList.ContainsKey(part))
                            {
                                toolDia = frzToolsList[part];
                            }
                            else
                            {
                                //throw new KeyNotFoundException("frzToolsList sözlüğünde " + part + " anahtarı bulunamadı.");
                            }
                        }
                    }

                    // Create CAD element
                    XmlElement cadElement = XmlHelper.AddElement(xmlDoc, kdtPanelFormatElement, "CAD");
                    XmlHelper.AddElement(xmlDoc, cadElement, "TypeNo", "1");
                    XmlHelper.AddElement(xmlDoc, cadElement, "TypeName", "Vertical Hole");
                    XmlHelper.AddElement(xmlDoc, cadElement, "X1", x);
                    XmlHelper.AddElement(xmlDoc, cadElement, "Y1", y);
                    XmlHelper.AddElement(xmlDoc, cadElement, "Depth", z);
                    XmlHelper.AddElement(xmlDoc, cadElement, "Diameter", toolDia);
                    XmlHelper.AddElement(xmlDoc, cadElement, "Enable", "1");
                }
                else if (trimmedLine.StartsWith("G182"))    // Left Pocket Hole
                {
                    if (!inG182Block) inG182Block = true;

                    string x, y, z, toolDia;
                    x = "";
                    y = "";
                    z = "";
                    toolDia = "";

                    string[] parts = trimmedLine.Split(' ');
                    foreach (string part in parts)
                    {
                        if (part == "G100") continue;
                        else if (part.StartsWith("X")) x = ProcessCoordinatePart(part, vars);
                        else if (part.StartsWith("Y")) y = ProcessCoordinatePart(part, vars);
                        else if (part.StartsWith("Z")) z = ProcessCoordinatePart(part, vars);
                        else if (part.StartsWith("T"))
                        {
                            if (frzToolsList.ContainsKey(part))
                            {
                                toolDia = frzToolsList[part];
                            }
                            else
                            {
                                //throw new KeyNotFoundException("frzToolsList sözlüğünde " + part + " anahtarı bulunamadı.");
                            }
                        }
                    }

                    string depth;
                    // The x var shows how deep it will enter on the panel. will be x tool depth

                    depth = x;
                    x = w;


                    // Create CAD element
                    XmlElement cadElement = XmlHelper.AddElement(xmlDoc, kdtPanelFormatElement, "CAD");
                    XmlHelper.AddElement(xmlDoc, cadElement, "TypeNo", "2");
                    XmlHelper.AddElement(xmlDoc, cadElement, "TypeName", "Horizontal Hole");
                    XmlHelper.AddElement(xmlDoc, cadElement, "X1", x);
                    XmlHelper.AddElement(xmlDoc, cadElement, "Y1", y);
                    XmlHelper.AddElement(xmlDoc, cadElement, "Z1", z);
                    XmlHelper.AddElement(xmlDoc, cadElement, "Quadrant", "1");
                    XmlHelper.AddElement(xmlDoc, cadElement, "Depth", depth);
                    XmlHelper.AddElement(xmlDoc, cadElement, "Diameter", toolDia);
                    XmlHelper.AddElement(xmlDoc, cadElement, "Enable", "1");

                }
                else if (trimmedLine.StartsWith("G183"))    // Right Pocket Hole
                {
                    if (!inG183Block) inG182Block = true;

                    string x, y, z, toolDia;
                    x = "";
                    y = "";
                    z = "";
                    toolDia = "";

                    string[] parts = trimmedLine.Split(' ');
                    foreach (string part in parts)
                    {
                        if (part == "G100") continue;
                        else if (part.StartsWith("X")) x = ProcessCoordinatePart(part, vars);
                        else if (part.StartsWith("Y")) y = ProcessCoordinatePart(part, vars);
                        else if (part.StartsWith("Z")) z = ProcessCoordinatePart(part, vars);
                        else if (part.StartsWith("T"))
                        {
                            if (frzToolsList.ContainsKey(part))
                            {
                                toolDia = frzToolsList[part];
                            }
                            else
                            {
                                //throw new KeyNotFoundException("frzToolsList sözlüğünde " + part + " anahtarı bulunamadı.");
                            }
                        }
                    }

                    string depth;
                    // The x side shows how deep it will enter on the panel. so the width will be depth of tool
                    depth = x;
                    x = "0";

                    // Create CAD element
                    XmlElement cadElement = XmlHelper.AddElement(xmlDoc, kdtPanelFormatElement, "CAD");
                    XmlHelper.AddElement(xmlDoc, cadElement, "TypeNo", "2");
                    XmlHelper.AddElement(xmlDoc, cadElement, "TypeName", "Horizontal Hole");
                    XmlHelper.AddElement(xmlDoc, cadElement, "X1", x);
                    XmlHelper.AddElement(xmlDoc, cadElement, "Y1", y);
                    XmlHelper.AddElement(xmlDoc, cadElement, "Z1", z);
                    XmlHelper.AddElement(xmlDoc, cadElement, "Quadrant", "3");
                    XmlHelper.AddElement(xmlDoc, cadElement, "Depth", depth);
                    XmlHelper.AddElement(xmlDoc, cadElement, "Diameter", toolDia);
                    XmlHelper.AddElement(xmlDoc, cadElement, "Enable", "1");
                }

            }

            return XmlHelper.GetFormattedXmlString(xmlDoc);
        }


        private async Task SaveXMLFile(string fileName, string xmlData, StorageFolder folder)
        {
            StorageFolder altKlasor = await folder.CreateFolderAsync("XML", CreationCollisionOption.OpenIfExists);
            StorageFile xmlDosya = await altKlasor.CreateFileAsync(fileName + ".xml", CreationCollisionOption.ReplaceExisting);
            await FileIO.WriteTextAsync(xmlDosya, xmlData);
        }

        static string ReplaceVariables(Dictionary<string, double> vars, string denklem)
        {
            foreach (var kvp in vars)
            {
                string varName = kvp.Key;
                double str = kvp.Value;
                denklem = denklem.Replace(varName, str.ToString());
            }

            return denklem;
        }


        static string Calculate(string formula)
        {
            Expression expr = new Expression(formula);
            return Convert.ToString(expr.Evaluate());
        }

        private string ProcessCoordinatePart(string part, Dictionary<string, double> vars)
        {
            string coordinateValue = part.Substring(1);

            // Remove the "=" character if it's present at the beginning
            if (coordinateValue.Length > 0 && coordinateValue[0] == '=')
            {
                coordinateValue = coordinateValue.Substring(1);
            }

            coordinateValue = ReplaceVariables(vars, coordinateValue);

            if (Regex.IsMatch(coordinateValue, @"[\+\-\*/]") && !double.TryParse(coordinateValue, out double result))
            {
                coordinateValue = Calculate(coordinateValue);
            }

            return coordinateValue;
        }

        async void ShowErrorMessage(string errorMessage)
        {
            var dialog = new MessageDialog(errorMessage, "Error");
            await dialog.ShowAsync();
        }

        private void TextBlock_SelectionChanged(object sender, RoutedEventArgs e)
        {

        }

        private void TextBlock_SelectionChanged_1(object sender, RoutedEventArgs e)
        {

        }

        private void TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {

        }
    }
}
