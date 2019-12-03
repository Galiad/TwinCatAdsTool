﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reactive.Linq;
using DynamicData;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Threading.Tasks;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;
using ReactiveUI;
using TwinCatAdsTool.Interfaces.Extensions;

namespace TwinCatAdsTool.Gui.ViewModels
{
    public class GraphViewModel : ViewModelBase
    {
        readonly Dictionary<string, List<DataPoint>> dataPoints = new Dictionary<string, List<DataPoint>>();

        private readonly SourceCache<SymbolObservationViewModel, string> symbolCache = new SourceCache<SymbolObservationViewModel, string>(x => x.Name);
        private PlotModel plotModel;
        private TimeSpan expiresAfter = TimeSpan.FromMinutes(10);
        private bool pause = false;


        public TimeSpan ExpiresAfter {
            get { return expiresAfter; }
            set
            {
                pause = true;
                expiresAfter = value;
                raisePropertyChanged();
                pause = false;
            }
        }


        public PlotModel PlotModel
        {
            get { return plotModel; }
            set
            {
                plotModel = value;
                raisePropertyChanged();
            }
        }

        private IObservableCache<SymbolObservationViewModel, string> SymbolCache => symbolCache.AsObservableCache();

        public void AddSymbol(SymbolObservationViewModel symbol)
        {
            var symbolInLineSeries = PlotModel.Series.FirstOrDefault(series => series.Title == symbol.Name);
            if (symbolInLineSeries == null)
            {
                symbolCache.AddOrUpdate(symbol);
            }
        }

        public override void Init()
        {
            PlotModel = CreateDefaultPlotModel();

            SymbolCache.Connect()
                .Transform(CreateSymbolLineSeries)
                .ObserveOnDispatcher()
                .DisposeMany()
                .Subscribe()
                .AddDisposableTo(Disposables);

            var axis = new DateTimeAxis
            {
                Position = AxisPosition.Bottom,
                StringFormat = "hh:mm:ss",
                IsZoomEnabled = false
            };

            PlotModel.Axes.Add(axis);
        }

        private static PlotModel CreateDefaultPlotModel()
        {
            return new PlotModel
            {
                LegendBorder = OxyColor.FromRgb(0x80, 0x80, 0x80),
                LegendBorderThickness = 1,
                LegendBackground = OxyColor.FromRgb(0xFF, 0xFF, 0xFF),
                LegendPosition = LegendPosition.LeftBottom
            };
        }

        public void RemoveSymbol(SymbolObservationViewModel symbol)
        {
            symbolCache.Remove(symbol.Name);

            var seriesToRemove = PlotModel.Series.FirstOrDefault(series => series.Title == symbol.Name);
            if (seriesToRemove != null)
            {
                PlotModel.Series.Remove(seriesToRemove);
            }

            var axisToRemove = PlotModel.Axes.FirstOrDefault(axis => axis.Key == symbol.Name);
            if (axisToRemove != null)
            {
                PlotModel.Axes.Remove(axisToRemove);
            }

            RescaleAxisDistances();
        }

        private IDisposable CreateSymbolLineSeries(SymbolObservationViewModel symbol)
        {
            var lineSeries = CreateLineSeriesAndAxis(symbol, out var disposable);

            RescaleAxisDistances();

            dataPoints[symbol.Name] = new List<DataPoint> {DateTimeAxis.CreateDataPoint(DateTime.Now, Convert.ToDouble(symbol.Value))};


            Observable.FromEventPattern<PropertyChangedEventHandler, PropertyChangedEventArgs>(
                    handler => handler.Invoke,
                    h => symbol.PropertyChanged += h,
                    h => symbol.PropertyChanged -= h)
                .Where(args => args.EventArgs.PropertyName == "Value" && pause == false)
                .ObserveOnDispatcher()
                .Subscribe(x => { UpdateDatapoints(symbol); }).AddDisposableTo(disposable);


            Observable.Interval(TimeSpan.FromSeconds(1))
                .Where(x => pause == false)
                .ObserveOnDispatcher()
                .Subscribe(x => { UpdateLineseries(symbol, lineSeries); })
                .AddDisposableTo(disposable);

            raisePropertyChanged("PlotModel");

            // Need to invalidate oxyplot graph after removal of line series in order to have it really removed from UI
            Disposable.Create(() => PlotModel.InvalidatePlot(true))
                .AddDisposableTo(disposable);

            return disposable;
        }

        private void UpdateLineseries(SymbolObservationViewModel symbol, LineSeries lineSeries)
        {
            var newPoints = dataPoints[symbol.Name]
                .Where(point => !lineSeries.Points.Select(oldPoint => oldPoint.X).Contains(point.X));
            if (!newPoints.Any() && dataPoints[symbol.Name].Any())
            {
                var lastPoint = dataPoints[symbol.Name].LastOrDefault();
                newPoints = new[] {DateTimeAxis.CreateDataPoint(DateTime.Now, lastPoint.Y)};
            }

            lineSeries.Points.AddRange(newPoints);

            var expireLimit = DateTimeAxis.ToDouble(DateTime.Now.Subtract(ExpiresAfter));
            lineSeries.Points.RemoveAll(point => point.X < expireLimit);

            PlotModel.InvalidatePlot(true);
            raisePropertyChanged("PlotModel");
        }

        private void UpdateDatapoints(SymbolObservationViewModel symbol)
        {
            var refreshTime = DateTime.Now;
            dataPoints[symbol.Name].Add(DateTimeAxis.CreateDataPoint(refreshTime, Convert.ToDouble(symbol.Value)));

            var expireLimit = DateTimeAxis.ToDouble(DateTime.Now.Subtract(ExpiresAfter));
            dataPoints[symbol.Name].RemoveAll(point => point.X < expireLimit);
        }

        private LineSeries CreateLineSeriesAndAxis(SymbolObservationViewModel symbol, out CompositeDisposable disposable)
        {
            var lineSeries = new LineSeries();
            lineSeries.Title = symbol.Name;

            var index = PlotModel.Axes.Count - 1;

            disposable = new CompositeDisposable();

            var axis = new LinearAxis
            {
                AxislineThickness = 2,
                AxislineColor = PlotModel.DefaultColors[index],
                MinorTickSize = 4,
                MajorTickSize = 7,
                TicklineColor = PlotModel.DefaultColors[index],
                TextColor = PlotModel.DefaultColors[index],
                AxisDistance = PlotModel.Axes.OfType<LinearAxis>().Count() * 50,
                Position = AxisPosition.Left,
                IsZoomEnabled = false,
                Key = symbol.Name,
                Tag = symbol.Name,
                MinimumPadding = 0.1,
                MaximumPadding = 0.1
            };

            lineSeries.YAxisKey = symbol.Name;

            PlotModel.Axes.Add(axis);
            PlotModel.Series.Add(lineSeries);
            return lineSeries;
        }

        private void RescaleAxisDistances()
        {
            for (var i = 0; i < PlotModel.Axes.OfType<LinearAxis>().Count(); i++)
            {
                PlotModel.Axes.OfType<LinearAxis>().Skip(i).First().AxisDistance = i * 50;
            }
        }
    }
}