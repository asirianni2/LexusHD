using System;
using System.Collections.Generic;
using System.Configuration;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using AAM.LexusRoad.FileTransform.Models;
using static System.String;

namespace AAM.LexusRoad.FileTransform
{
    class Program
    {
        public static void Main(string[] args)
        {
            // Get the input csv folder path and output asn1 folder path from App.config;
            var csvDir = ConfigurationManager.AppSettings["csv.path"];
            var asn1Dir = ConfigurationManager.AppSettings["asn1.path"];

            var revisionId = ConfigurationManager.AppSettings["revision"];
            var laneWidth = ConfigurationManager.AppSettings["lane_width"];

            var intersections = new List<Intersection>();

            try
            {
                if (!Directory.Exists(csvDir))
                {
                    Directory.CreateDirectory(csvDir);
                }

                if (!Directory.Exists(asn1Dir))
                {
                    Directory.CreateDirectory(asn1Dir);
                }

                // Get the list of names of all the csv files;
                var csvFiles = Directory.GetFiles(csvDir, "*.csv")
                    .Select(Path.GetFileName)
                    .ToArray();

                foreach (var csvFile in csvFiles)
                {
                    // var intersectionX = new Intersection { };
                    var csvPath = csvDir + csvFile;
                    var fileNamePrefix = csvFile.Substring(0, csvFile.IndexOf(".", StringComparison.Ordinal));  // strip the ".csv";
                    var intersectionName = GetIntersectionName(csvFile);

                    // Check if the intersection already existed; If not, create one
                    var intersectionX = intersections.FirstOrDefault(item => item.IntersectionNo == intersectionName);
                    if (intersectionX == null)
                    {
                        intersectionX = new Intersection
                        {
                            IntersectionNo = intersectionName,
                            Revision = revisionId,
                            LaneWidth = laneWidth,
                            Lanes = new List<Lane>()
                        };

                        intersections.Add(intersectionX);
                    }

                    if (fileNamePrefix.Contains("geo"))
                    {
                        intersectionX = ReadDataFromGeoCsv(csvPath,intersectionX);
                    }
                    else if (fileNamePrefix.Contains("_con"))
                    {
                        intersectionX = ReadDataFromConCsv(csvPath, intersectionX);
                    }

                }

                foreach (var intersectionX in intersections)
                {
                    var intersectionDir = asn1Dir + intersectionX.IntersectionNo + "\\";

                    // Create a folder for each intersection;
                    if (!Directory.Exists(intersectionDir))
                    {
                        Directory.CreateDirectory(intersectionDir);
                    }

                    CalculateDeltaValues(intersectionX);

                    ConvertToAsn1(csvDir, asn1Dir, intersectionX, intersectionDir);
                }

            }
            catch (Exception ex)
            {
                //throw new Exception("error occurred!", ex);
                Console.WriteLine(ex.StackTrace);
            }

            Console.WriteLine("Tasks finished. Application exits in 2s.");
            Thread.Sleep(1000);
            Console.WriteLine("Tasks finished. Application exits in 1s.");
            Thread.Sleep(1000);
            Console.WriteLine("Tasks finished. Application terminated.");
            Environment.Exit(0);
        }

        /*
         *  Read data from a "XXXXgeo.csv" file and save in the intersection object.
         */
        public static Intersection ReadDataFromGeoCsv(string csvPath, Intersection intersectionX)
        {
            try
            {
                // Read the first line for the RefPoint.
                StreamReader sr = new StreamReader(csvPath);
                string refLine = sr.ReadLine();
                string[] refData = refLine?.Split(',');
                intersectionX.RefPoint = new RefPoint { IntersectionNo = intersectionX.IntersectionNo };
                if (refData != null && refData.Length >= 6)
                {
                    intersectionX.RefPoint.East = refData[1];
                    intersectionX.RefPoint.North = refData[2];
                    intersectionX.RefPoint.Elevation = refData[3];
                    intersectionX.RefPoint.Latitude = refData[4];
                    intersectionX.RefPoint.Longitude = refData[5];
                }

                // Read every other line from the 2nd line.
                // Each line is a LandNode object.
                // If The LaneNodeID is 1, then it's starting from a new lane and will create a new Lane object.
                Lane laneX = new Lane { LaneNodes = new List<LaneNode>() };
                while (!sr.EndOfStream)
                {
                    var csvLine = sr.ReadLine();
                    string[] values = csvLine?.Split(',');
                    if (values != null && values.Length >= 13 && values[1] != "NA")
                    {
                        int nodeId = int.Parse(values[3]);
                        var nodeX = new LaneNode
                        {
                            LaneId = values[2],
                            LaneNodeId = nodeId,
                            DeltaX = double.Parse(values[9]),
                            DeltaY = double.Parse(values[10]),
                            DeltaWidth = double.Parse(values[8]),
                            DeltaElevation = double.Parse(values[7]),
                            East = values[5],
                            North = values[6],
                            Latitude = values[11],
                            Longitude = values[12]
                        };

                        // When LaneNodeId is 1, starting from a new lane.
                        if (nodeId == 1)            
                        {
                            // If the laneX already has some node(s), add the existing laneX to the intersection.
                            if (laneX.LaneNodes != null && laneX.LaneNodes.Count > 0)
                            {
                                intersectionX.Lanes.Add(laneX);
                            }

                            laneX = new Lane
                            {
                                IntersectionNo = intersectionX.IntersectionNo,
                                ApproachType = values[1],
                                LaneId = values[2],
                                LaneManouver = values[4],
                                LaneNodes = new List<LaneNode>(),
                                LaneConnections = new List<LaneConnectsTo>()
                            };
                        }

                        // Add each node to the specific lane.
                        laneX.LaneNodes.Add(nodeX);
                    }
                }

                // Add the last lane to the intersection.
                if (laneX.LaneNodes != null && laneX.LaneNodes.Count > 0)
                {
                    intersectionX.Lanes.Add(laneX);
                }

                sr.Close();
            }
            catch (Exception ex)
            {
                //throw new Exception("error occurred!", ex);
                Console.WriteLine(ex.StackTrace);
            }
            
            return intersectionX;
        }

        /*
         *  Read data from a "XXXX_con.csv" file and save in the intersection object.
         */
        public static Intersection ReadDataFromConCsv(string csvPath, Intersection intersectionX)
        {
            try
            {
                List<LaneConnectsTo> connections = new List<LaneConnectsTo>();
                StreamReader sr = new StreamReader(csvPath);
                while (!sr.EndOfStream)
                {
                    var csvLine = sr.ReadLine();
                    string[] values = csvLine?.Split(',');
                    if (values != null && values.Length >= 6)
                    {
                        // Read each line and put values to LaneConnectsTo objects.
                        var connectsToX = new LaneConnectsTo
                        {
                            LandId = values[0],
                            ConnectingLaneId = values[1],
                            ApproachId = values[2],
                            ExitId = values[3],
                            SignalGroup = values[4],
                            Manouver = values[5]
                        };

                        // Add the connection object to the List each time.
                        connections.Add(connectsToX);
                    }
                }

                sr.Close();

                foreach (var connectionX in connections)
                {
                    // Find the lane from the intersection, then add the connection object to the Lane.LaneConnections list.
                    foreach (var laneX in intersectionX.Lanes)
                    {
                        if (laneX.LaneId == connectionX.LandId)
                        {
                            laneX.LaneConnections.Add(connectionX);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                //throw new Exception("error occurred!", ex);
                Console.WriteLine(ex.StackTrace);
            }

            return intersectionX;
        }

        /*
         *  Calculate the DeltaX, DeltaY, DWidth and DElevation values for each node.
         */
        public static Intersection CalculateDeltaValues(Intersection intersectionX)
        {
            var laneWidth = double.Parse(ConfigurationManager.AppSettings["lane_width"]);
            var refElevation = double.Parse(intersectionX.RefPoint.Elevation);

            for (var lx =0; lx < intersectionX.Lanes.Count; lx++)
            {
                var laneX = intersectionX.Lanes[lx];

                double preWidth = 0, preElevation = 0, preX = 0, preY = 0;  // values for the previous node.
                double curWidth = 0, curElevation = 0, curX = 0, curY = 0;  // values for the current node.

                for (var nx = 0; nx < intersectionX.Lanes[lx].LaneNodes.Count; nx++)
                {
                    var nodeX = intersectionX.Lanes[lx].LaneNodes[nx];
                    curWidth = nodeX.DeltaWidth;
                    curElevation = nodeX.DeltaElevation;
                    curX = nodeX.DeltaX;
                    curY = nodeX.DeltaY;

                    if (nx == 0)
                    {
                        // First point dWidth = [(FirstPointWidth*100)-310], and round it to the nearest integer.
                        intersectionX.Lanes[lx].LaneNodes[0].DeltaWidth = Math.Round((curWidth * 100) - laneWidth);
                        // First point dElevation = [(FirstPointElevation - RefPointElevation)*100], then round to the nearest integer.
                        intersectionX.Lanes[lx].LaneNodes[0].DeltaElevation = Math.Round((curElevation - refElevation) * 100);
                        // First point DeltaX and DeltaY will be (X*100), then round to the nearest integer. 
                        intersectionX.Lanes[lx].LaneNodes[0].DeltaX = Math.Round(curX * 100);
                        intersectionX.Lanes[lx].LaneNodes[0].DeltaY = Math.Round(curY * 100);
                    }
                    else
                    {
                        // Since second point dWidth = [(SecondPoint - FirstPoint)*100], then round to the nearest integer.
                        intersectionX.Lanes[lx].LaneNodes[nx].DeltaWidth = Math.Round((curWidth - preWidth) * 100);
                        // Since second point dElevation = [(SecondPoint - FirstPoint)*100], then round to the nearest integer.
                        intersectionX.Lanes[lx].LaneNodes[nx].DeltaWidth = Math.Round((curElevation - preElevation) * 100);
                        // Since second point DeltaX and DeltaY = [(SecondPoint - FirstPoint)*100], then round to the nearest integer.
                        intersectionX.Lanes[lx].LaneNodes[nx].DeltaX = Math.Round((curX - preX) * 100);
                        intersectionX.Lanes[lx].LaneNodes[nx].DeltaY = Math.Round((curY - preY) * 100);
                    }

                    preWidth = curWidth;
                    preElevation = curElevation;
                    preX = curX;
                    preY = curY;
                }
            }

            return intersectionX;
        }

        /*
         *  Convert from the content of csv files, which belong to the intersection, to an asn1 file;
         */
        public static void ConvertToAsn1(string csvDir, string asn1Dir, Intersection intersectionX, string intersectionDir)
        {
            // Read configs from App.config;
            var revisionId = ConfigurationManager.AppSettings["revision"];
            var laneWidth = ConfigurationManager.AppSettings["lane_width"];

            var outFile = intersectionX.IntersectionNo + "_rev" + revisionId + ".asn1";
            var outputPath = intersectionDir + outFile;

            var asnTemplatePath = DirProject(2) + @"\fileTemplates\templateASN1.asn1";
            var laneTemplatePath = DirProject(2) + @"\fileTemplates\template_Lane.asn1";
            var laneNodeTemplatePath = DirProject(2) + @"\fileTemplates\template_Lane_Node.asn1";
            var laneConnectionTemplatePath = DirProject(2) + @"\fileTemplates\template_Lane_ConnectsTo.asn1";

            var laneNodeTemplate = File.ReadAllText(laneNodeTemplatePath);
            var laneConnectionTemplate = File.ReadAllText(laneConnectionTemplatePath);
            var laneTemplate = File.ReadAllText(laneTemplatePath);
            var asnTemplate = File.ReadAllText(asnTemplatePath);

            var laneContent = "";
            for (var lx = 0; lx < intersectionX.Lanes.Count; lx++)
            {
                var laneX = intersectionX.Lanes[lx];
                var oneLane = Copy(laneTemplate);
                var laneApproachId = " ";

                // Step1 - Get the connectsTo data and write to the lane template.
                if (laneX.LaneConnections.Count > 0)
                {
                    laneApproachId = laneX.LaneConnections[0].ApproachId;
                    var laneConnectionContent = "";
                    for (var ix = 0; ix < laneX.LaneConnections.Count; ix++)
                    {
                        var connectionX = laneX.LaneConnections[ix];
                        var oneConnection = Copy(laneConnectionTemplate);
                        oneConnection = oneConnection.Replace("[CONNECT_LANE_ID]", connectionX.ConnectingLaneId);
                        oneConnection = oneConnection.Replace("[CONNECT_MANEUVER_TYPE]", "{maneuver" + connectionX.Manouver + "Allowed}");
                        oneConnection = oneConnection.Replace("[CONNECT_SIGNAL_GROUP]", connectionX.SignalGroup);

                        if (ix > 0)
                        {
                            laneConnectionContent += ",\n";
                        }

                        laneConnectionContent += oneConnection;
                    }

                    oneLane = oneLane.Replace("[TEMPLATE_LANE_CONNECTSTO]", laneConnectionContent);
                }
                else
                {
                    // No connection data for this lane, remove the template placeholder.
                    oneLane = oneLane.Replace("[TEMPLATE_LANE_CONNECTSTO]", "");
                }

                // Step2 - Get the LaneNode data and write to the lane template.
                if (laneX.LaneNodes.Count > 0)
                {
                    var laneNodeContent = "";
                    for (var i = 0; i < laneX.LaneNodes.Count; i++)
                    {
                        var laneNodeX = laneX.LaneNodes[i];
                        var oneNode = Copy(laneNodeTemplate);
                        oneNode = oneNode.Replace("[NODE_DELTA_X]", laneNodeX.DeltaX.ToString(CultureInfo.CurrentCulture));
                        oneNode = oneNode.Replace("[NODE_DELTA_Y]", laneNodeX.DeltaY.ToString(CultureInfo.CurrentCulture));
                        oneNode = oneNode.Replace("[NODE_DELTA_WIDTH]", laneNodeX.DeltaWidth.ToString(CultureInfo.CurrentCulture));
                        oneNode = oneNode.Replace("[NODE_DELTA_ELEVATION]", laneNodeX.DeltaElevation.ToString(CultureInfo.CurrentCulture));

                        if (i > 0)  // Since the second node in the list, add a comma and a new line.
                        {
                            laneNodeContent += ",\n";
                        }

                        laneNodeContent += oneNode;
                    }

                    oneLane = oneLane.Replace("[TEMPLATE_LANE_NODES]", laneNodeContent);
                }
                else
                {
                    oneLane = oneLane.Replace("[TEMPLATE_LANE_NODES]", "");
                }


                // Step3 - populate the common attributes for a lane.
                oneLane = oneLane.Replace("[TEMPLATE_LANE_ID]", laneX.LaneId);
                switch (laneX.ApproachType.Trim())
                {
                    case "Enter":
                        oneLane = oneLane.Replace("[TEMPLATE_ACCESS_APPROACH]", "ingressApproach " + laneApproachId + ",");
                        oneLane = oneLane.Replace("[TEMPLATE_PATH_USE]", "ingressPath");
                        oneLane = oneLane.Replace("[TEMPLATE_SHARED_WITH]", "ingressPath");
                        oneLane = oneLane.Replace("[TEMPLATE_LANE_TYPE]", "vehicle : {}");
                        break;
                    case "Exit":
                        oneLane = oneLane.Replace("[TEMPLATE_ACCESS_APPROACH]", "egressApproach " + laneApproachId + ",");
                        oneLane = oneLane.Replace("[TEMPLATE_PATH_USE]", "egressPath");
                        oneLane = oneLane.Replace("[TEMPLATE_SHARED_WITH]", "egressPath");
                        oneLane = oneLane.Replace("[TEMPLATE_LANE_TYPE]", "vehicle : {}");
                        break;
                    case "Crosswalk":
                        oneLane = oneLane.Replace("[TEMPLATE_ACCESS_APPROACH]", "");
                        oneLane = oneLane.Replace("[TEMPLATE_PATH_USE]", "");
                        oneLane = oneLane.Replace("[TEMPLATE_SHARED_WITH]", "cyclistVehicleTraffic, pedestrianTraffic");
                        oneLane = oneLane.Replace("[TEMPLATE_LANE_TYPE]", "crosswalk : {bicyleUseAllowed, hasPushToWalkButton}");
                        break;
                    default:
                        oneLane = oneLane.Replace("[TEMPLATE_ACCESS_APPROACH]", "");
                        oneLane = oneLane.Replace("[TEMPLATE_PATH_USE]", "");
                        oneLane = oneLane.Replace("[TEMPLATE_SHARED_WITH]", "");
                        oneLane = oneLane.Replace("[TEMPLATE_LANE_TYPE]", "vehicle : {}");
                        break;
                }


                if (lx > 0) // Since the second lane, add a comma and a new line.
                {
                    laneContent += ",\n";
                }

                laneContent += oneLane;
            }

            var asnContent = Copy(asnTemplate);
            asnContent = asnContent.Replace("[TEMPLATE_MSG_REVISION]", revisionId);
            asnContent = asnContent.Replace("[TEMPLATE_FIELD_ID]", intersectionX.IntersectionNo);
            asnContent = asnContent.Replace("[TEMPLATE_REVISION]", revisionId);
            asnContent = asnContent.Replace("[TEMPLATE_REF_LAT]", intersectionX.RefPoint.Latitude);
            asnContent = asnContent.Replace("[TEMPLATE_REF_LONG]", intersectionX.RefPoint.Longitude);
            asnContent = asnContent.Replace("[TEMPLATE_REF_ELEVATION]", intersectionX.RefPoint.Elevation);
            asnContent = asnContent.Replace("[TEMPLATE_LANE_WIDTH]", laneWidth);
            asnContent = asnContent.Replace("[TEMPLATE_LANES]", laneContent);


            
            if (File.Exists(outputPath))
            {
                File.Delete(outputPath);
                Console.WriteLine("File " + outFile + " removed and replaced!");
            }

            File.WriteAllText(outputPath, asnContent);
        }

        /*
         *  Get the project directory path, based from bin\Debug\ folder.
         */
        public static string DirProject(int dirLevel)
        {
            var dirProject = Directory.GetCurrentDirectory();

            for (var cnt = 0; cnt < dirLevel; cnt++)
            {
                dirProject = dirProject.Substring(0, dirProject.LastIndexOf(@"\", StringComparison.Ordinal));
            }

            return dirProject;
        }

        /*
         *  Extract and return the intersection name from full file name.
         */
        public static string GetIntersectionName(string fileName)
        {
            var resultString = Regex.Match(fileName, @"\d+").Value;

            return resultString;
        }

        /*
         *  Assert whether a string contains only digits.
         */
        public static bool StringIsDigit(string str)
        {
            return str.All(x => (x >= '0' && x <= '9'));
        }
    }
}