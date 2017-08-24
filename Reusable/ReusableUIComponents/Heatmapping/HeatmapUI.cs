﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Media;
using Brushes = System.Drawing.Brushes;
using Color = System.Drawing.Color;
using FontFamily = System.Drawing.FontFamily;
using FontStyle = System.Drawing.FontStyle;
using Point = System.Drawing.Point;

namespace ReusableUIComponents.Heatmapping
{
    /// <summary>
    /// Displays complicated many dimension pivot Aggregate graphs in an understandable format.  Requires a result data table that contains an axis in the first column of hte data table
    /// followed by any number (usually high e.g. 500+) additional columns which contain values that correspond to the axis.  A typical usage of this control would be to display drug 
    /// prescriptions by month where there are thousands of different prescribeable drugs.  
    /// 
    /// The HeatmapUI renders each column as a row of heat map with each cell in the column as a 'pixel' (where the pixel width depends on the number of increments in the axis).  The color
    /// of each pixel ranges from blue to red (with 0 appearing as black).  The effect of this is to show the distribution of popular vs rare pivot values across time (or whatever the axis is).
    /// 
    /// You can use this to visualise high dimensionality data that is otherwise incomprehensible in AggregateGraph
    /// </summary>
    public partial class HeatmapUI : UserControl
    {


        /*/////////////////////////////////////////EXPECTED DATA TABLE FORMAT/////////////////////////////
        * Date   | HeatLine1 | HeatLine2| HeatLine3 | HeatLine4 | etc
        * 2001   |    30     |   40     |    30     |   40      | ...
        * 2002   |    10     |   40     |    20     |   43      | ...
        * 2003   |    11     |   10     |    50     |   10      | ...
        * 2004   |    5      |   20     |    30     |   45      | ...
        * 2005   |    -3     |   10     |    30     |   44      | ...
        * 2006   |    17     |   99     |    10     |   45      | ...
        * 2007   |    19     |   40     |    30     |   40      | ...
        * ...    |   ...     |    ...   |   ...     |   ...     | ...
        * 
        * */

        //Control Layout:

        //////////////////////////////////////////////////////////////////////////////////////////////
        //     <<axis labels on first visible line of control>>                      | Labels go here
        //                                                                           |
        //              Heat Lines                                                   |
        //              Heat Lines                                                   |
        //              Heat Lines                                                   |
        //              ...                        plot area                         | 
        //                                                                           |
        //                                                                           |
        //                                                                           |
        //                                                                           |
        //////////////////////////////////////////////////////////////////////////////////////////////


        ///Table is interpreted in the following way: 
        /// - First column is the axis in direction X (horizontally) containing (in order) the axis label values that will be each pixel in each heat lane
        /// - Each subsequent column (HeatLine1, HeatLine2 etc above) is a horizontal line of the heatmap with each pixel intensity being determined by the value on the corresponding date (in the first column)
        
        private RainbowColorPicker _rainbow = new RainbowColorPicker(NumberOfColors);
        private const double MinPixelHeight = 12.0;
        private const double MaxPixelHeight = 20.0;

        private const double MaxLabelsWidth = 150;
        private double _currentLabelsWidth = 0;

        private object oDataTableLock = new object();

        public HeatmapUI()
        {
            InitializeComponent();

        }

        public void SetDataTable(DataTable dataTable)
        {
            lock (oDataTableLock)
            {

                _dataTable = dataTable;

                //skip the first column (which will be the X axis values)  then compute the maximum value in any cell in the data table, this is the brightest pixel in heatmap
                //the minimum value will be the darkest pixel
            
                _maxValueInDataTable = double.MinValue;
                _minValueInDataTable = double.MaxValue;

                for (int x = 0; x < _dataTable.Rows.Count; x++)
                    for (int y = 1; y < _dataTable.Columns.Count; y++)
                    {

                        var cellValue = ToDouble(_dataTable.Rows[x][y]);

                        if (cellValue < _minValueInDataTable)
                            _minValueInDataTable = cellValue;

                        if (cellValue > _maxValueInDataTable)
                            _maxValueInDataTable = cellValue;
                    }

                Height = (int)Math.Max(Height, ((_dataTable.Columns.Count) * MinPixelHeight));
            }
            
            Invalidate();
        }

        private double ToDouble(object o)
        {
            return o == DBNull.Value ? 0 : Convert.ToDouble(o);
        }

        private DataTable _dataTable;
        private double _maxValueInDataTable;
        private double _minValueInDataTable;
        private bool _crashedPainting;
        

        private const int NumberOfColors = 256;

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            Invalidate();
        }


        ToolTip tt = new ToolTip();

        private int toolTipDelayInTicks = 500;
        private Point _lastHoverPoint;
        private int _lastHoverTickCount;
        private bool _useEntireControlAsVisibleArea = false;


        private void hoverToolTipTimer_Tick(object sender, EventArgs e)
        {
            var pos = PointToClient(Cursor.Position);
            
            //if we moved
            if(!_lastHoverPoint.Equals(pos))
            {
                _lastHoverPoint = pos;
                _lastHoverTickCount = Environment.TickCount;

                tt.Hide(this);
                return;
            }

            //we didn't move, have we been here a while?
            if (Environment.TickCount - _lastHoverTickCount < toolTipDelayInTicks)
                return;//no

            //yes we have been here a while so show the tool tip
            _lastHoverTickCount = Environment.TickCount;
            object value = null;

            lock (oDataTableLock)
                value = GetValueFromClientPosition(pos);

            //there wasn't anything to display anyway
            if(value == null)
                return;
            
            //show the tool tip
            tt.Show(value.ToString(), this, new Point(pos.X,pos.Y - 10));//allow room for cusor to not overdraw the tool tip
            
        }

        private object GetValueFromClientPosition(Point pos)
        {
            if (_dataTable == null)
                return null;

            if (pos.X < 0 || pos.Y < 0)
                return null;

            //pointer is to the right of the entire control
            if (pos.X > Width)
                return null;

            double pixelHeight = GetHeatPixelHeight();
            double pixelWidth = GetHeatPixelWidth();


            int dataTableCol = (int) (1 +( pos.Y/pixelHeight)); //heat map line number + 1 because first column is the axis label
            int dataTableRow = (int) (pos.X/pixelWidth); //the pixel width corresponds to the number of axis values in the first column

            if (dataTableCol >= _dataTable.Columns.Count)
                return null;

            //return the label since they are on the right of the control
            if (dataTableRow >= _dataTable.Rows.Count)
                return _dataTable.Columns[dataTableCol].ColumnName;


            return _dataTable.Rows[dataTableRow][dataTableCol];
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            
            if(_dataTable == null)
                return;
            if (_crashedPainting)
                return;
            try
            {
                lock (oDataTableLock)
                {
                    //draw background
                    e.Graphics.FillRectangle(Brushes.White, new Rectangle(0,0,Width,Height));

                    //decide how tall to make pixels
                    double heatPixelHeight = GetHeatPixelHeight();

                    //based on the height of the line what text font will fit into that line?
                    Font font = GetFontSizeThatWillFitPixelHeight(heatPixelHeight, e.Graphics);

                    //now we know the Font to use, figure out the width of the longest piece of text when rendered with the Font (with a sensible max, we aren't allowing war and peace into this label)
                    _currentLabelsWidth = GetLabelWidth(e.Graphics, font);

                    //now that we know the width of the labels work out the width of each pixel to fill the rest of the controls area with heat pixels / axis
                    double heatPixelWidth = GetHeatPixelWidth();

                    var brush = new SolidBrush(Color.Black);
                    
                    //for each line of pixels in heatmap
                    for (int x = 0; x < _dataTable.Rows.Count; x++)
                    {
                        //draw the line this way -------------> with pixels of width heatPixelWidth/Height
                    
                        //skip the first y value which is the x axis value
                        for (int y = 1; y < _dataTable.Columns.Count; y++)
                        {
                            //the value we are drawing
                            var cellValue = ToDouble(_dataTable.Rows[x][y]);
                        
                            //if the cell value is 0 render it as black
                            if (Math.Abs(cellValue - _minValueInDataTable) < 0.0000000001 && Math.Abs(_minValueInDataTable) < 0.0000000001)
                                brush.Color = Color.Black;
                            else
                            {
                                double brightness = (cellValue - _minValueInDataTable) / (_maxValueInDataTable - _minValueInDataTable);
                                int brightnessIndex = (int)(brightness * (NumberOfColors - 1));

                                brush.Color = _rainbow.Colors[brightnessIndex];
                            }

                            e.Graphics.FillRectangle(brush, (float)(x * heatPixelWidth), (float)(y * heatPixelHeight), (float)heatPixelWidth, (float)heatPixelHeight);
                        }
                    }
                
                    double labelStartX = Width - _currentLabelsWidth;
                
                
                    //draw the labels
                    for (int i = 1; i < _dataTable.Columns.Count; i++)
                    {
                        double labelStartY = (i)*heatPixelHeight;
                    
                        var name = _dataTable.Columns[i].ColumnName;

                        e.Graphics.DrawString(name, font, Brushes.Black, new PointF((float)labelStartX, (float)labelStartY));
                    }
    
                    double lastAxisStart = -500;
                    double lastAxisLabelWidth = -500;

                    var visibleArea = _useEntireControlAsVisibleArea ? new Rectangle(0,0,Width,Height) : FormsHelper.GetVisibleArea(this);
                    
                    
                    int visibleClipBoundsTop = visibleArea.Top;

                    //now draw the axis 
                    //axis starts at the first visible pixel
                    double axisYStart = Math.Max(0, visibleClipBoundsTop);
                    
                    e.Graphics.FillRectangle(Brushes.White, 0, (int)axisYStart, Width, (int)heatPixelHeight);

                    //draw the axis labels
                    for (int i = 0; i < _dataTable.Rows.Count; i++)
                    {
                        double axisXStart = i * heatPixelWidth;

                        //skip labels if the axis would result in a label overdrawing it's mate
                        if (axisXStart  < lastAxisStart + lastAxisLabelWidth)
                            continue;

                        lastAxisStart = axisXStart;
                    
                        var label = _dataTable.Rows[i][0].ToString();

                        //draw the axis label text with 1 pixel left and right so that there is space for the axis black line
                        e.Graphics.DrawString(label, font, Brushes.Black, new PointF((float) axisXStart + 1, (float) axisYStart));
                        lastAxisLabelWidth = (int)e.Graphics.MeasureString(label, font).Width + 2;


                        //draw axis black line
                        e.Graphics.DrawLine(Pens.Black, new PointF((float) axisXStart, (float)(axisYStart)), new PointF((float) axisXStart, Height));
                    }
                }

            }
            catch (Exception exception)
            {
                _crashedPainting = true;
                ExceptionViewer.Show(exception);
            }

        }

        private double GetLabelWidth(Graphics g, Font font)
        {
            var nameStrings = _dataTable.Columns.Cast<DataColumn>().Select(c => c.ColumnName);
            var longestString = nameStrings.Aggregate("", (max, cur) => max.Length > cur.Length ? max : cur);
            var longestStringWidth = g.MeasureString(longestString, font).Width;

            if (VerticalScroll.Visible)
                longestStringWidth += SystemInformation.VerticalScrollBarWidth;

            return Math.Min(MaxLabelsWidth, longestStringWidth);
        }

        private double GetHeatPixelWidth()
        {
            double plotAreaWidth = Width - _currentLabelsWidth;
            return plotAreaWidth/_dataTable.Rows.Count;
        }

        /// <summary>
        /// Gets a suitable size to render each heat line respecting the controls Height and the number of dimensions in the DataTable.  Bounded by min and max PixelHeights (see consts)
        /// 
        /// </summary>
        /// <returns></returns>
        private double GetHeatPixelHeight()
        {
            double plotAreaHeight = Height;
            double numberOfDimensions = _dataTable.Columns.Count; //first column is the X axis value

            return Math.Min(MaxPixelHeight, Math.Max(MinPixelHeight, plotAreaHeight / numberOfDimensions));
        }

        private Font GetFontSizeThatWillFitPixelHeight(double heightInPixels, Graphics graphics)
        {
            Font font;
            double emSize = heightInPixels;
            do
            {
                font = new Font(new FontFamily("Tahoma"), (float)(emSize -= 0.5), FontStyle.Regular);

            } while (graphics.MeasureString("testing", font).Height > heightInPixels);

            return font;
        }




        public void CalculateLayout()
        {
            
        }

        public void Clear()
        {
            
            lock(oDataTableLock)
                _dataTable = null;
        }

        public bool HasDataTable()
        {
            return _dataTable != null;
        }

        public Bitmap GetImage(int maxHeight)
        {
            int h = Math.Min(maxHeight,Height);

            bool isClipped = maxHeight < Height;
            
            var bmp = new Bitmap(Width, h);

            _useEntireControlAsVisibleArea = true;

            DrawToBitmap(bmp, new Rectangle(0, 0, Width, h));

            _useEntireControlAsVisibleArea = false;

            if (isClipped)
            {
                //number of heat map lines
                int numberOfHeatLinesVisible = (int) (h/GetHeatPixelHeight());

                //total number of heatmap lines
                int totalHeatMapLinesAvailable = _dataTable.Columns.Count -1;

                if (numberOfHeatLinesVisible < totalHeatMapLinesAvailable)
                {
                    //add a note saying to user data has been clipped
                    string clippedRowsComment = (totalHeatMapLinesAvailable - numberOfHeatLinesVisible) + " more rows clipped";
                    var g = Graphics.FromImage(bmp);

                    var fontSize = g.MeasureString(clippedRowsComment, Font);

                    //centre it on the bottom of the image
                    g.FillRectangle(Brushes.WhiteSmoke,0, h - fontSize.Height,fontSize.Width,fontSize.Height);
                    g.DrawString(clippedRowsComment,Font,Brushes.Black,0,h-fontSize.Height);

                }



            }

            return bmp;
        }

        public void SaveImage(string heatmapPath, ImageFormat imageFormat)
        {
            var bmp = new Bitmap(Width, Height);

            _useEntireControlAsVisibleArea = true;

            DrawToBitmap(bmp, new Rectangle(0,0,Width,Height));
            bmp.Save(heatmapPath,imageFormat);

            _useEntireControlAsVisibleArea = false;
        }
    }
}
