namespace Mappy.UI.Controls
{
    using System;
    using System.Collections.Generic;
    using System.Drawing;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Windows.Forms;

    using Mappy.Models;

    public partial class MapViewPanel : UserControl
    {
        private static object timerlock = new object();

        private static List<CancellationTokenSource> debounceCancelTokens = new List<CancellationTokenSource>();

        private IMapViewViewModel model;

        private Point oldAutoScrollPos;

        private bool shouldScrollAtEdge = true;

        public MapViewPanel()
        {
            this.InitializeComponent();

            this.mapView.Layers.Add(new DummyLayer());
            this.mapView.Layers.Add(new DummyLayer());
            this.mapView.Layers.Add(new DummyLayer());
            this.mapView.Layers.Add(new DummyLayer());
        }

        public void SetModel(IMapViewViewModel model)
        {
            model.CanvasSize.Subscribe(x => this.mapView.CanvasSize = x);
            model.ViewportLocation.Subscribe(x => this.mapView.AutoScrollPosition = x);

            model.ItemsLayer.Subscribe(x => this.mapView.Layers[0] = x);
            model.VoidLayer.Subscribe(x => this.mapView.Layers[1] = x);
            this.mapView.Layers[2] = model.GridLayer;
            this.mapView.Layers[3] = model.GuidesLayer;

            this.model = model;
        }

        private void MapViewDragDrop(object sender, DragEventArgs e)
        {
            var loc = this.mapView.ToVirtualPoint(this.mapView.PointToClient(new Point(e.X, e.Y)));
            this.model.DragDrop(e.Data, loc);
        }

        private void MapViewMouseDown(object sender, MouseEventArgs e)
        {
            var loc = this.mapView.ToVirtualPoint(e.Location);
            this.model.MouseDown(loc);
            this.shouldScrollAtEdge = true;
        }

        private void MapViewMouseMove(object sender, MouseEventArgs e)
        {
            var loc = this.mapView.ToVirtualPoint(e.Location);
            this.model.MouseMove(loc);
            var currentScrollPos = this.mapView.AutoScrollOffset;
            Console.WriteLine("Moved " + e.Location.X + " " + e.Location.Y);
            Console.WriteLine("X " + loc.X + " " + loc.Y);
            if (this.shouldScrollAtEdge && loc.X > this.Width * 0.9)
            {
                // Console.WriteLine("X " + loc.X + " " + loc.Y);
                this.DebounceAutoScroll(new Point(-currentScrollPos.X + 10, currentScrollPos.Y));
            }

            if (this.shouldScrollAtEdge && loc.Y > this.Height * 0.9)
            {
                // Console.WriteLine("X " + loc.X + " " + loc.Y);
                this.DebounceAutoScroll(new Point(currentScrollPos.X, -currentScrollPos.Y + 10));
            }
        }

        private void MapViewMouseUp(object sender, MouseEventArgs e)
        {
            this.model.MouseUp();
            //this.shouldScrollAtEdge = false;
        }

        private void MapViewKeyDown(object sender, KeyEventArgs e)
        {
            this.model.KeyDown(e.KeyCode);
        }

        private void MapViewLeave(object sender, EventArgs e)
        {
            this.model.LeaveFocus();
        }

        private void MapViewSizeChanged(object sender, EventArgs e)
        {
            // this null check has to be here
            // since it seems this event fires during construction,
            // before we get a chance to assign the model.
            this.model?.ClientSizeChanged(this.mapView.ClientSize);
        }

        private void MapViewDragEnter(object sender, DragEventArgs e)
        {
            e.Effect = DragDropEffects.Copy;
        }

        private void MapViewPaint(object sender, PaintEventArgs e)
        {
            // We listen to paint to detect when scroll position has changed.
            // We could use the scroll event, but this only detects
            // scrollbar interaction, and won't catch other scrolling
            // such as mouse wheel scrolling.
            var pos = this.mapView.AutoScrollPosition;
            if (pos != this.oldAutoScrollPos)
            {
                var loc = new Point(pos.X * -1, pos.Y * -1);
                this.model.ScrollPositionChanged(loc);
                this.oldAutoScrollPos = pos;
            }
        }

        private void DebounceAutoScroll(Point newScrollLoc)
        {
            // 4.0 so old, no access to Task.Run() or Task.Delay()
            object taskObj = Task.Factory.StartNew(() =>
            {
                var newDebounceToken = new CancellationTokenSource();
                lock (timerlock)
                {
                    this.CancelDebouncingTokens();
                    debounceCancelTokens.Add(newDebounceToken);
                }

                Thread.Sleep(500);

                if (!newDebounceToken.IsCancellationRequested)
                {
                    this.CancelDebouncingTokens();
                    debounceCancelTokens = new List<CancellationTokenSource>();
                    lock (timerlock)
                    {
                        this.mapView.AutoScrollPosition = newScrollLoc;

                        this.mapView.VerticalScroll.Value = newScrollLoc.Y;
                        this.mapView.VerticalScroll.Value = newScrollLoc.Y;

                        this.mapView.HorizontalScroll.Value = newScrollLoc.X;
                        this.mapView.HorizontalScroll.Value = newScrollLoc.X;

                        this.model.ScrollPositionChanged(newScrollLoc);
                        this.oldAutoScrollPos = newScrollLoc;
                    }
                }
            });
        }

        private void CancelDebouncingTokens()
        {
            foreach (var token in debounceCancelTokens)
            {
                if (!token.IsCancellationRequested)
                {
                    token.Cancel();
                }
            }
        }
    }
}
