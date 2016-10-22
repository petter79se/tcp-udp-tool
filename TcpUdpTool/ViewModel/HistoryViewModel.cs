﻿using System;
using System.Collections.Concurrent;
using System.Windows.Input;
using System.Windows.Threading;
using TcpUdpTool.Model;
using TcpUdpTool.Model.Data;
using TcpUdpTool.Model.Formatter;
using TcpUdpTool.Model.Util;
using TcpUdpTool.ViewModel.Helper;
using TcpUdpTool.ViewModel.Item;
using TcpUdpTool.ViewModel.Reusable;
using TcpUdpTool.ViewModel.Reuseable;

namespace TcpUdpTool.ViewModel
{
    public class HistoryViewModel : ObservableObject, IContentChangedHelper
    {

        #region private members

        private BlockingCollection<Piece> _incomingQueue;
        private RateMonitor _rateMonitor;
        private IFormatter _formatter;
        private DispatcherTimer _updateTimer;
        private long _lastPackageDiscarded;

        #endregion

        #region public properties

        private string _header;
        public string Header
        {
            get { return _header; }
            set
            {
                _header = value;
                OnPropertyChanged(nameof(Header));
            }
        }

        private BatchObservableCollection<ConversationItemViewModel> _conversation;
        public BatchObservableCollection<ConversationItemViewModel> Conversation
        {
            get { return _conversation; }
        }

        private string _totalReceived;
        public string TotalReceived
        {
            get { return _totalReceived; }
            set
            {
                if(_totalReceived != value)
                {
                    _totalReceived = value;
                    OnPropertyChanged(nameof(TotalReceived));
                }
            }
        }

        private string _rateReceive;
        public string RateReceive
        {
            get { return _rateReceive; }
            set
            {
                if (_rateReceive != value)
                {
                    _rateReceive = value;
                    OnPropertyChanged(nameof(RateReceive));
                }
            }
        }

        private string _totalSent;
        public string TotalSent
        {
            get { return _totalSent; }
            set
            {
                if (_totalSent != value)
                {
                    _totalSent = value;
                    OnPropertyChanged(nameof(TotalSent));
                }
            }
        }

        private string _rateSend;
        public string RateSend
        {
            get { return _rateSend; }
            set
            {
                if (_rateSend != value)
                {
                    _rateSend = value;
                    OnPropertyChanged(nameof(RateSend));
                }
            }
        }

        private bool _plainTextSelected;
        public bool PlainTextSelected
        {
            get { return _plainTextSelected; }
            set
            {
                if (value != _plainTextSelected)
                {
                    _plainTextSelected = value;
                    OnPropertyChanged(nameof(PlainTextSelected));
                }
            }
        }

        private bool _hexSelected;
        public bool HexSelected
        {
            get { return _hexSelected; }
            set
            {
                if (value != _hexSelected)
                {
                    _hexSelected = value;
                    OnPropertyChanged(nameof(HexSelected));
                }
            }
        }

        private bool _statisticsSelected;
        public bool StatisticsSelected
        {
            get { return _statisticsSelected; }
            set
            {
                if(value != _statisticsSelected)
                {
                    _statisticsSelected = value;
                    OnPropertyChanged(nameof(StatisticsSelected));
                }
            }
        }

        private bool _packageDiscardedWarning;
        public bool PackageDiscardedWarning
        {
            get { return _packageDiscardedWarning; }
            set
            {
                if(_packageDiscardedWarning != value)
                {
                    _packageDiscardedWarning = value;
                    OnPropertyChanged(nameof(PackageDiscardedWarning));
                }
            }
        }

        #endregion

        #region public commands

        public ICommand ClearCommand
        {
            get { return new DelegateCommand(Clear); }
        }

        public ICommand ViewChangedCommand
        {
            get { return new DelegateCommand(ViewChanged); }
        }

        #endregion

        #region public events

        public event Action ContentChanged;

        #endregion

        #region constructors

        public HistoryViewModel()
        {
            _rateMonitor = new RateMonitor();
            _incomingQueue = new BlockingCollection<Piece>(100);
            _conversation = new BatchObservableCollection<ConversationItemViewModel>();
            _conversation.CollectionChanged += (sender, e) => ContentChanged?.Invoke();
            _formatter = new PlainTextFormatter();
            _updateTimer = new DispatcherTimer();
            _updateTimer.Interval = new TimeSpan(0, 0, 0, 0, 50);
            _updateTimer.Tick += (sender, arg) => OnUpdateUI();

            PlainTextSelected = true;

            _rateMonitor.Start();
            _updateTimer.Start();

            
            Properties.Settings.Default.PropertyChanged +=
                (sender, e) =>
                {
                    if (e.PropertyName == nameof(Properties.Settings.Default.HistoryEntries))
                    {
                        // just switch out the queue, don't care about potentially lost items.
                        _incomingQueue = new BlockingCollection<Piece>(Properties.Settings.Default.HistoryEntries);
                    }
                    else if(e.PropertyName == nameof(Properties.Settings.Default.Encoding))
                    {
                        ViewChanged();
                    }
                    else if (e.PropertyName == nameof(Properties.Settings.Default.HistoryInfoTimestamp) ||
                             e.PropertyName == nameof(Properties.Settings.Default.HistoryInfoIpAdress))
                    {
                        // force update.
                        ViewChanged();
                    }
                };
           
        }

        #endregion

        #region public functions

        public void Append(Piece msg)
        {
            if(msg.IsReceived)
            {
                _rateMonitor.NoteReceived(msg);
            }
            else
            {
                _rateMonitor.NoteSent(msg);
            }

            if(!_incomingQueue.TryAdd(msg))
            {
                _lastPackageDiscarded = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
            }
        }

        private void Clear()
        {
            Conversation.Clear();

            if(StatisticsSelected)
            {
                _rateMonitor.Reset();
                OnUpdateUI();
            }
        }

        private void ViewChanged()
        {           
            if (PlainTextSelected)
            {
                _formatter = new PlainTextFormatter(SettingsUtils.GetEncoding());
                ApplyFormatter(_formatter);
            }
            else if (HexSelected)
            {
                _formatter = new HexFormatter();
                ApplyFormatter(_formatter);
            }
        }

        #endregion

        #region private functions

        private void ApplyFormatter(IFormatter formatter)
        {
            foreach(var item in _conversation)
            {
                item.SetFormatter(formatter);
            }
        }

        private void OnUpdateUI()
        {
            if (StatisticsSelected)
            {
                PackageDiscardedWarning = false;

                TotalReceived = StringFormatUtils.GetSizeAsString(_rateMonitor.TotalReceivedBytes);
                RateReceive = StringFormatUtils.GetRateAsString(_rateMonitor.CurrentReceiveRate) + " (" + StringFormatUtils.GetSizeAsString(_rateMonitor.CurrentReceiveRate / 8) + "/s)";
                TotalSent = StringFormatUtils.GetSizeAsString(_rateMonitor.TotalSentBytes);
                RateSend = StringFormatUtils.GetRateAsString(_rateMonitor.CurrentSendRate) + " (" + StringFormatUtils.GetSizeAsString(_rateMonitor.CurrentSendRate / 8) + "/s)"; ;

                // dont update conversation view if more than 100kbps 
                // is received and view not selected.
                if (_rateMonitor.CurrentReceiveRate > 100000)
                {
                    return;
                }
            }
            else
            {
                PackageDiscardedWarning = ((DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond) - _lastPackageDiscarded) < 10000;
            }

            _conversation.BeginBatch();

            Piece msg = null;
            while(_incomingQueue.TryTake(out msg))
            {
                _conversation.Add(new ConversationItemViewModel(msg, _formatter));
            }

            while(_conversation.Count > _incomingQueue.BoundedCapacity)
            {
                _conversation.RemoveAt(0);
            }

            _conversation.EndBatch();
        }

        #endregion

    }
}
