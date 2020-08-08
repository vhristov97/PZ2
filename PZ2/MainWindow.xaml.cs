using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Xml;
using WpfApp1.Model;

namespace PZ2
{
    public enum Straight { NOT, HORIZONTAL, VERTICAL}
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public const int gridSize = 270;

        bool[,] grid = new bool[gridSize, gridSize];
        private double minX, maxX, minY, maxY;
        
        Ellipse clickedEllipse;
        Polyline clickedPolyline;
        //private int notDrawn = 0;
        
        Dictionary<long, Tuple<IPowerEntity, Ellipse>> nodeDictionary;
        Dictionary<Polyline, LineEntity> lineDictionary;
        
        List<LineEntity> lineList;
        List<Polyline>[,] canvasPolylines = new List<Polyline>[gridSize, gridSize];

        public MainWindow()
        {
            InitializeComponent();

            nodeDictionary = new Dictionary<long, Tuple<IPowerEntity, Ellipse>>();
            lineDictionary = new Dictionary<Polyline, LineEntity>();
            lineList = new List<LineEntity>();

            ParseXml("Geographic.xml");

            this.DataContext = this;
        }

        //From UTM to Latitude and longitude in decimal
        private void ToLatLon(double utmX, double utmY, int zoneUTM, out double latitude, out double longitude)
		{
            // minX = 45.189624220491204    maxX = 45.32874395177668
            // minY = 19.72727551428336     maxY = 19.950953770332024
            bool isNorthHemisphere = true;

			var diflat = -0.00066286966871111111111111111111111111;
			var diflon = -0.0003868060578;

			var zone = zoneUTM;
			var c_sa = 6378137.000000;
			var c_sb = 6356752.314245;
			var e2 = Math.Pow((Math.Pow(c_sa, 2) - Math.Pow(c_sb, 2)), 0.5) / c_sb;
			var e2cuadrada = Math.Pow(e2, 2);
			var c = Math.Pow(c_sa, 2) / c_sb;
			var x = utmX - 500000;
			var y = isNorthHemisphere ? utmY : utmY - 10000000;

			var s = ((zone * 6.0) - 183.0);
			var lat = y / (c_sa * 0.9996);
			var v = (c / Math.Pow(1 + (e2cuadrada * Math.Pow(Math.Cos(lat), 2)), 0.5)) * 0.9996;
			var a = x / v;
			var a1 = Math.Sin(2 * lat);
			var a2 = a1 * Math.Pow((Math.Cos(lat)), 2);
			var j2 = lat + (a1 / 2.0);
			var j4 = ((3 * j2) + a2) / 4.0;
			var j6 = ((5 * j4) + Math.Pow(a2 * (Math.Cos(lat)), 2)) / 3.0;
			var alfa = (3.0 / 4.0) * e2cuadrada;
			var beta = (5.0 / 3.0) * Math.Pow(alfa, 2);
			var gama = (35.0 / 27.0) * Math.Pow(alfa, 3);
			var bm = 0.9996 * c * (lat - alfa * j2 + beta * j4 - gama * j6);
			var b = (y - bm) / v;
			var epsi = ((e2cuadrada * Math.Pow(a, 2)) / 2.0) * Math.Pow((Math.Cos(lat)), 2);
			var eps = a * (1 - (epsi / 3.0));
			var nab = (b * (1 - epsi)) + lat;
			var senoheps = (Math.Exp(eps) - Math.Exp(-eps)) / 2.0;
			var delt = Math.Atan(senoheps / (Math.Cos(nab)));
			var tao = Math.Atan(Math.Cos(delt) * Math.Tan(nab));

			longitude = ((delt * (180.0 / Math.PI)) + s) + diflon;
			latitude = ((lat + (1 + e2cuadrada * Math.Pow(Math.Cos(lat), 2) - (3.0 / 2.0) * e2cuadrada * Math.Sin(lat) * Math.Cos(lat) * (tao - lat)) * (tao - lat)) * (180.0 / Math.PI)) + diflat;
        }

        private int RangeConverter(double oldMin, double oldMax, double newMin, double newMax, double oldValue)
        {
            double oldRange = oldMax - oldMin;
            double newRange = newMax - newMin;

            return Convert.ToInt32((((oldValue - oldMin) * newRange) / oldRange) + newMin);
        }

        private Tuple<int, int> GetCanvasPosition(double x, double y)
        {
            int canvasY = RangeConverter(0, (grid.GetLength(1) - 1), 10, canvas.ActualHeight - 10, y);  // preracunava tacku iz domena 0-99 na na ekran 
            int canvasX = RangeConverter(0, (grid.GetLength(0) - 1), 10, canvas.ActualWidth - 10, x);   // 10 je da tacke ne bi bile na samoj ivici

            return new Tuple<int, int>(canvasX, canvasY);
        }
        // proverava dal je tacka (x, y) slobodna, ako nije, proverava okolne tacke i menja x i y
        private bool GridAvailability(ref int x, ref int y)
        {
            bool done = false;  // pronasao je slobodno mesto u matrici, povratna
            if (grid[x, y] == false)    // x i y iz xml-a su slobodni
            {
                grid[x, y] = true;
                done = true;
            }
            else
            {
                int direction;              // 0 - UP, 1 - RIGHT,  2 -DOWN, 3 -LEFT
                int i = 0;
                int j = 0;
                int stepCount = 1;          // broj koraka u jednom pravcu
                bool sameStepCount = false; // broj koraka u jednom pravcu se menja nakon druge promene pravca
                bool finished = false;      // oznacava da li jos ima da iterira ili je zavrsio

                while (!finished)
                {
                    for (direction = 0; direction < 4; direction++)             // svi pravci
                    {
                        for (int k = 0; k < stepCount; k++)                     // iterira kroz svaki korak
                        {
                            switch (direction)                                  // pomeranje za 1 korak u zavisnosti od pravca
                            {
                                case (0): i--; break;
                                case (1): j++; break;
                                case (2): i++; break;
                                case (3): j--; break;
                            }

                            if (i == -3 && j == -3)                             // dosao je do kraja matrice koja obuhvata
                            {                                                   // sve susedne cvorove
                                finished = true;
                                break;
                            }

                            if (x + j > (grid.GetLength(1) - 1) || x + j < 0)   // izlazi iz matrice po x-u
                            {
                                j += stepCount - 1;                             // preskace taj pravac, -1 jer je vec povecao u switch
                                break;
                            }

                            if (y + i > (grid.GetLength(0) - 1) || y + i < 0)   // izlazi iz matrice po y-u
                            {
                                i += stepCount - 1;
                                break;
                            }

                            if (grid[x + j, y + i] == false)                    // pronasao slobodno mesto u matrici
                            {
                                x += j;
                                y += i;
                                grid[x, y] = true;

                                done = true;                               
                                finished = true;
                                break;
                            }
                        }

                        if (finished)
                        {
                            break;
                        }

                        if (sameStepCount)                                      // ako je broj koraka isti kao u prethodnoj iteraciji 
                        {                                                       // u narednoj je potrebno povecati broj koraka za 1
                            sameStepCount = false;
                            stepCount++;
                        }
                        else                                                    // ako nije isti kao u prethodnoj iteraciji
                        {                                                       // u narednoj ce biti
                            sameStepCount = true;
                        }
                    }
                }
            }

            /*if (!done)
            {
                notDrawn++;
            }*/

            return done;
        }

        private void ParseXml(string xml)
        {
            XmlDocument xmlDoc = new XmlDocument();
            xmlDoc.Load(xml);
            XmlNodeList nodeList;
            
            double noviX, noviY;
            bool first = true;

            maxX = 0;
            minX = 0;
            maxY = 0;
            minY = 0;

            nodeList = xmlDoc.DocumentElement.SelectNodes("/NetworkModel/Substations/SubstationEntity");
            foreach (XmlNode node in nodeList)
            {
                SubstationEntity sub = new SubstationEntity();
                ParseNode(node, sub);

                if (first)
                {
                    ToLatLon(sub.X, sub.Y, 34, out minY, out minX);
                    maxX = minX;
                    maxY = minY;

                    first = false;
                }
                else
                {
                    ToLatLon(sub.X, sub.Y, 34, out noviY, out noviX);

                    if(noviX > maxX)
                        maxX = noviX;
                    else if(noviX < minX)
                        minX = noviX;

                    if (noviY > maxY)
                        maxY = noviY;
                    else if (noviY < minY)
                        minY = noviY;
                }

                nodeDictionary.Add(sub.Id, new Tuple<IPowerEntity, Ellipse>(sub, null));
            }

            nodeList = xmlDoc.DocumentElement.SelectNodes("/NetworkModel/Nodes/NodeEntity");
            foreach (XmlNode node in nodeList)
            {
                NodeEntity nodeEntity = new NodeEntity();
                ParseNode(node, nodeEntity);

                ToLatLon(nodeEntity.X, nodeEntity.Y, 34, out noviY, out noviX);

                if (noviX > maxX)
                    maxX = noviX;
                else if (noviX < minX)
                    minX = noviX;

                if (noviY > maxY)
                    maxY = noviY;
                else if (noviY < minY)
                    minY = noviY;

                nodeDictionary.Add(nodeEntity.Id, new Tuple<IPowerEntity, Ellipse>(nodeEntity, null));
            }

            nodeList = xmlDoc.DocumentElement.SelectNodes("/NetworkModel/Switches/SwitchEntity");
            foreach (XmlNode node in nodeList)
            {
                SwitchEntity switchEntity = new SwitchEntity();
                ParseNode(node, switchEntity);

                ToLatLon(switchEntity.X, switchEntity.Y, 34, out noviY, out noviX);

                if (noviX > maxX)
                    maxX = noviX;
                else if (noviX < minX)
                    minX = noviX;

                if (noviY > maxY)
                    maxY = noviY;
                else if (noviY < minY)
                    minY = noviY;

                nodeDictionary.Add(switchEntity.Id, new Tuple<IPowerEntity, Ellipse>(switchEntity, null));
            }

            nodeList = xmlDoc.DocumentElement.SelectNodes("/NetworkModel/Lines/LineEntity");
            foreach (XmlNode node in nodeList)
            {
                LineEntity lineEntity = new LineEntity();
                ParseLine(node, lineEntity);

                if(nodeDictionary.ContainsKey(lineEntity.FirstEnd) && nodeDictionary.ContainsKey(lineEntity.SecondEnd))
                {
                    lineList.Add(lineEntity);
                }
            }
        }

        private void ParseLine(XmlNode node, LineEntity l)
        {
            l.Id = long.Parse(node.SelectSingleNode("Id").InnerText);
            l.Name = node.SelectSingleNode("Name").InnerText;
            if (node.SelectSingleNode("IsUnderground").InnerText.Equals("true"))
            {
                l.IsUnderground = true;
            }
            else
            {
                l.IsUnderground = false;
            }
            l.R = float.Parse(node.SelectSingleNode("R").InnerText);
            l.ConductorMaterial = node.SelectSingleNode("ConductorMaterial").InnerText;
            l.LineType = node.SelectSingleNode("LineType").InnerText;
            l.ThermalConstantHeat = long.Parse(node.SelectSingleNode("ThermalConstantHeat").InnerText);
            l.FirstEnd = long.Parse(node.SelectSingleNode("FirstEnd").InnerText);
            l.SecondEnd = long.Parse(node.SelectSingleNode("SecondEnd").InnerText);

            foreach (XmlNode pointNode in node.ChildNodes[9].ChildNodes) // 9 posto je Vertices 9. node u jednom line objektu
            {
                WpfApp1.Model.Point p = new WpfApp1.Model.Point();

                p.X = double.Parse(pointNode.SelectSingleNode("X").InnerText);
                p.Y = double.Parse(pointNode.SelectSingleNode("Y").InnerText);
            }
        }

        private void ParseNode(XmlNode node, IPowerEntity entity)
        {
            entity.Id = long.Parse(node.SelectSingleNode("Id").InnerText);
            entity.Name = node.SelectSingleNode("Name").InnerText;
            entity.X = double.Parse(node.SelectSingleNode("X").InnerText);
            entity.Y = double.Parse(node.SelectSingleNode("Y").InnerText);
            if (entity.GetType() == typeof(SwitchEntity))
                ((SwitchEntity)entity).Status = node.SelectSingleNode("Status").InnerText;
        }

        private void btnLoad_Click(object sender, RoutedEventArgs e)
		{
            btnLoad.IsEnabled = false;
            slider.IsEnabled = true;

            DrawNodes();

            List<LineEntity> notDrawnLines;

            /*while(lineList.Count > 0)
            {
                LineEntity line = FindShortestLine();*/
            notDrawnLines = PlotAndDrawLines(lineList, true);

            PlotAndDrawLines(notDrawnLines, false);
        }

        private void DrawNodes()
        {      
            double newX, newY;
            Tuple<int, int> canvasPosition;
            string tooltip;

            Dictionary<long, Tuple<IPowerEntity, Ellipse>> updatedNodes = new Dictionary<long, Tuple<IPowerEntity, Ellipse>>();

            foreach (KeyValuePair<long, Tuple<IPowerEntity, Ellipse>> node in nodeDictionary)
            {
                tooltip = "ID: " + node.Key + "\nName: " + node.Value.Item1.Name;
                if (node.Value.GetType() == typeof(SwitchEntity))
                    tooltip += "\nStatus: " + ((SwitchEntity)node.Value.Item1).Status;

                ToLatLon(node.Value.Item1.X, node.Value.Item1.Y, 34, out newY, out newX);

                int gridX = RangeConverter(minX, maxX, 0, (grid.GetLength(1) - 1), newX);
                int gridY = RangeConverter(minY, maxY, 0, (grid.GetLength(0) - 1), newY);

                if (GridAvailability(ref gridX, ref gridY))
                {
                    node.Value.Item1.X = gridX;
                    node.Value.Item1.Y = gridY;

                    Ellipse el = new Ellipse();
                    ToolTip t = new ToolTip();

                    t.Content = tooltip;
                    el.ToolTip = t;
     
                    if (node.Value.Item1.GetType() == typeof(SubstationEntity))
                        el.Fill = Brushes.Red;
                    else if (node.Value.Item1.GetType() == typeof(NodeEntity))
                        el.Fill = Brushes.Green;
                    else
                        el.Fill = Brushes.Blue;

                    el.Height = 2;
                    el.Width = 2;

                    canvasPosition = GetCanvasPosition(node.Value.Item1.X, node.Value.Item1.Y);

                    Canvas.SetZIndex(el, 1);
                    Canvas.SetLeft(el, canvasPosition.Item1);
                    Canvas.SetBottom(el, canvasPosition.Item2); 

                    canvas.Children.Add(el);

                    updatedNodes.Add(node.Value.Item1.Id, new Tuple<IPowerEntity, Ellipse>(node.Value.Item1, el));
                }
            }

            nodeDictionary = updatedNodes;
        }

        private List<LineEntity> PlotAndDrawLines(List<LineEntity> lines, bool initialRun)
        {
            List<LineEntity> notDrawnLines = new List<LineEntity>();

            foreach (LineEntity line in lines)
            {
                if (!(nodeDictionary[line.FirstEnd].Item1.ConnectedTo.Contains(line.SecondEnd)) && !(nodeDictionary[line.SecondEnd].Item1.ConnectedTo.Contains(line.FirstEnd)))
                {
                    List<Tuple<int, int>> linePoints = BFS(line, initialRun);

                    if (linePoints.Count > 0)
                    {
                        lineDictionary.Add(DrawLine(linePoints, "ID: " + line.Id + "\nName: " + line.Name/*, initialRun*/), line);

                        nodeDictionary[line.FirstEnd].Item1.ConnectedTo.Add(line.SecondEnd);
                        nodeDictionary[line.SecondEnd].Item1.ConnectedTo.Add(line.FirstEnd);
                    }
                    else
                        notDrawnLines.Add(line);
                }
            }

            return notDrawnLines;
        }

        private Polyline DrawLine(List<Tuple<int, int>> linePoints, string tooltip/*, bool initialRun*/)
        {
            /*Straight s;
            int index;
            bool intersection = false;*/

            Polyline polyline = new Polyline();
            Tuple<int, int> canvasPosition;

            polyline.Stroke = Brushes.Black;
            polyline.StrokeThickness = 1;

            ToolTip t = new ToolTip();
            t.Content = tooltip;
            polyline.ToolTip = t;

            foreach (Tuple<int, int> point in linePoints)
            {
                /*index = linePoints.IndexOf(point);
                s = Straight.NOT;*/

                canvasPosition = GetCanvasPosition(point.Item1, point.Item2);
                polyline.Points.Add(new System.Windows.Point(canvasPosition.Item1 + 1, canvas.Height - canvasPosition.Item2 - 1));

                /*if (!initialRun)
                {
                    if (index > 0 && index < linePoints.Count - 1)
                    {
                        if (linePoints[index - 1].Item1 == point.Item1 && Math.Abs(linePoints[index - 1].Item2 - point.Item2) == 1)
                        {
                            if (linePoints[index + 1].Item1 == point.Item1 && Math.Abs(linePoints[index - 1].Item2 - point.Item2) == 1)
                            {
                                s = Straight.VERTICAL;
                            }
                        }
                        else if (linePoints[index - 1].Item2 == point.Item2 && Math.Abs(linePoints[index - 1].Item1 - point.Item1) == 1)
                        {
                            if (linePoints[index + 1].Item2 == point.Item2 && Math.Abs(linePoints[index - 1].Item1 - point.Item1) == 1)
                            {
                                s = Straight.HORIZONTAL;
                            }
                        }

                        if (s != Straight.NOT)
                            intersection = CheckIntersection(s, point.Item1, point.Item2);
                    }
                }
                
                if (intersection)
                {
                    Ellipse el = new Ellipse();

                    el.Fill = Brushes.Transparent;
                    el.Stroke = Brushes.Purple;
                    el.Width = 3;
                    el.Height = 3;

                    Canvas.SetLeft(el, canvasPosition.Item1);
                    Canvas.SetBottom(el, canvasPosition.Item2);

                    canvas.Children.Add(el);
                }*/

                if(canvasPolylines[point.Item2, point.Item1] == null)
                {
                    canvasPolylines[point.Item2, point.Item1] = new List<Polyline>();
                }

                canvasPolylines[point.Item2, point.Item1].Add(polyline);
            }

            canvas.Children.Add(polyline);

            return polyline;
        }

        private LineEntity FindShortestLine()
        {
            double lineLength;
            double shortestLength = Math.Abs(nodeDictionary[lineList[0].FirstEnd].Item1.X - nodeDictionary[lineList[0].SecondEnd].Item1.X)
                + Math.Abs(nodeDictionary[lineList[0].FirstEnd].Item1.Y - nodeDictionary[lineList[0].SecondEnd].Item1.Y);
            LineEntity shortestLine = lineList[0];

            foreach (LineEntity line in lineList)
            {
                IPowerEntity first = nodeDictionary[line.FirstEnd].Item1;
                IPowerEntity second = nodeDictionary[line.SecondEnd].Item1;

                lineLength = Math.Abs(first.X - second.X) + Math.Abs(first.Y - second.Y);

                if(lineLength < shortestLength)
                {
                    shortestLength = lineLength;
                    shortestLine = line;
                }
            }

            return shortestLine;
        }

        public List<Tuple<int, int>> BFS(LineEntity line, bool initialRun)
        {
            Tuple<int, int> startingPoint = new Tuple<int, int>((int)nodeDictionary[line.FirstEnd].Item1.X, (int)nodeDictionary[line.FirstEnd].Item1.Y);
            Tuple<int, int> endPoint = new Tuple<int, int>((int)nodeDictionary[line.SecondEnd].Item1.X, (int)nodeDictionary[line.SecondEnd].Item1.Y);
            Tuple<int, int> currentPoint;
            Queue<Tuple<int, int>> toBeChecked = new Queue<Tuple<int, int>>();

            List<Tuple<int, int>> ret = new List<Tuple<int, int>>();

            bool reachedEnd = false;

            bool[,] visited = new bool[grid.GetLength(0), grid.GetLength(1)];
            Tuple<int, int>[,] prev = new Tuple<int, int>[grid.GetLength(0), grid.GetLength(1)];

            toBeChecked.Enqueue(startingPoint);
            visited[startingPoint.Item2, startingPoint.Item1] = true;

            while (toBeChecked.Count > 0)
            {
                currentPoint = toBeChecked.Dequeue();
                if (currentPoint.Item1 == endPoint.Item1 && currentPoint.Item2 == endPoint.Item2)
                {
                    reachedEnd = true;
                    break;
                }

                ExploreNeighbours(currentPoint, ref visited, ref prev, toBeChecked, initialRun);
            }

            if (reachedEnd)
            {
                ret.Add(endPoint);
                Tuple<int, int> current = prev[endPoint.Item2, endPoint.Item1];

                while(!(current.Item1 == startingPoint.Item1 && current.Item2 == startingPoint.Item2))
                {
                    ret.Add(current);
                    current = prev[current.Item2, current.Item1];
                }

                ret.Add(startingPoint);
            }

            return ret;
        }

        private void ExploreNeighbours(Tuple<int, int> currentPoint, ref bool[,] visited,
            ref Tuple<int, int>[,] prev, Queue<Tuple<int, int>> toBeChecked, bool initialRun)
        {
            int x = currentPoint.Item1;
            int y = currentPoint.Item2;
            int nextX, nextY;
            Tuple<int, int> previousPoint = prev[y, x];
            Straight s = Straight.NOT;

            for (int direction = 0; direction < 4; direction++)
            {
                nextX = x;
                nextY = y;

                switch (direction)                               
                {
                    case (0): nextY = y - 1; 
                        if(previousPoint != null && previousPoint.Item2 == y + 1)
                        {
                            s = Straight.VERTICAL;
                        }
                        break;
                    case (1): nextX = x + 1;
                        if (previousPoint != null && previousPoint.Item1 == x - 1)
                        {
                            s = Straight.HORIZONTAL;
                        }
                        break;
                    case (2): nextY = y + 1;
                        if (previousPoint != null && previousPoint.Item2 == y - 1)
                        {
                            s = Straight.VERTICAL;
                        }
                        break;
                    case (3): nextX = x - 1;
                        if (previousPoint != null && previousPoint.Item1 == x + 1)
                        {
                            s = Straight.HORIZONTAL;
                        }
                        break;
                }

                if (nextX < 0 || nextY < 0)
                    continue;
                else if (nextX > visited.GetLength(1) - 1 || nextY > visited.GetLength(0) - 1)
                    continue;

                if (visited[nextY, nextX])
                    continue;

                if (initialRun)
                {
                    if (s != Straight.NOT)
                    {
                        if (CheckIntersection(s, x, y))
                            continue;
                    }
                }

                toBeChecked.Enqueue(new Tuple<int, int>(nextX, nextY));
                visited[nextY, nextX] = true;
                prev[nextY, nextX] = currentPoint;
            }
        }

        public bool CheckIntersection(Straight s, int x, int y)
        {
            bool intersection = false;

            if (canvasPolylines[y, x] != null)
            {
                foreach (Polyline p in canvasPolylines[y, x])
                {
                    if (s == Straight.HORIZONTAL)
                    {
                        if(y - 1 >= 0 && canvasPolylines[y - 1, x] != null && canvasPolylines[y - 1, x].Contains(p))
                        {
                            if (y + 1 < grid.GetLength(0) && canvasPolylines[y + 1, x] != null && canvasPolylines[y + 1, x].Contains(p))
                            {
                                intersection = true;
                                break;
                            }
                        }
                    }
                    else
                    {
                        if (x - 1 >= 0 && canvasPolylines[y, x - 1] != null && canvasPolylines[y, x - 1].Contains(p))
                        {
                            if (x + 1 < grid.GetLength(1) && canvasPolylines[y, x + 1] != null && canvasPolylines[y, x + 1].Contains(p))
                            {
                                intersection = true;
                                break;
                            }
                        }
                    }
                }
            }

            return intersection;
        }

        private void canvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if(clickedEllipse != null)
            {
                clickedEllipse.Width = clickedEllipse.Width / 10;
                clickedEllipse.Height = clickedEllipse.Height / 10;
                Canvas.SetZIndex(clickedEllipse, 1);

                clickedEllipse = null;
            }

            if(e.OriginalSource is Ellipse)
            {
                clickedEllipse = (Ellipse)e.OriginalSource;

                Canvas.SetZIndex(clickedEllipse, 2);
                clickedEllipse.Width = clickedEllipse.Width * 10;
                clickedEllipse.Height = clickedEllipse.Height * 10;
            }
        }

        private void canvas_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if(clickedPolyline != null)
            {
                long first = lineDictionary[clickedPolyline].FirstEnd;
                long second = lineDictionary[clickedPolyline].SecondEnd;

                if (nodeDictionary[first].Item1.GetType() == typeof(SubstationEntity))
                    nodeDictionary[first].Item2.Fill = Brushes.Red;
                else if (nodeDictionary[first].Item1.GetType() == typeof(NodeEntity))
                    nodeDictionary[first].Item2.Fill = Brushes.Green;
                else
                    nodeDictionary[first].Item2.Fill = Brushes.Blue;

                if (nodeDictionary[second].Item1.GetType() == typeof(SubstationEntity))
                    nodeDictionary[second].Item2.Fill = Brushes.Red;
                else if (nodeDictionary[second].Item1.GetType() == typeof(NodeEntity))
                    nodeDictionary[second].Item2.Fill = Brushes.Green;
                else
                    nodeDictionary[second].Item2.Fill = Brushes.Blue;

                clickedPolyline = null;
            }

            if(e.OriginalSource is Polyline)
            {
                clickedPolyline = (Polyline)e.OriginalSource;

                long first = lineDictionary[clickedPolyline].FirstEnd;
                long second = lineDictionary[clickedPolyline].SecondEnd;

                nodeDictionary[first].Item2.Fill = Brushes.Yellow;
                nodeDictionary[second].Item2.Fill = Brushes.Yellow;
            }
        }
    }
}
