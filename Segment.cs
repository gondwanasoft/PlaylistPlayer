using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LibVLCSharp.WPF.Sample
{
    public class Segment : INotifyPropertyChanged {
        private string _filepath;   // with folder
        private long _start = 0,
                     _end = 0;      // :stop-time msec; _end=0 implies play to end of file
        private int _count = 1; // repeats-1
        private bool _slow = false;     // if true, :rate=0.5
        private bool _fastSeek = false;     // :input-fast-seek
        private bool? _mute = null;         // :no-audio

        public Segment() { }
        public Segment(Segment seg) {
            _filepath = seg._filepath;
            _start = seg._start;
            _end = seg._end;
            _count = seg._count;
            _slow = seg._slow;
            _fastSeek = seg._fastSeek;
            _mute = seg._mute;
        }
        public string Filepath {
            get { return _filepath; }
            set { _filepath = value; NotifyPropertyChanged("Filename"); }
        }
        public string Filename {
            get { return System.IO.Path.GetFileName(_filepath); }
        }
        public long Start
        {
            get { return _start; }
            set { _start = value; NotifyPropertyChanged("StartString"); }
        }
        public string StartString
        {
            get { return MainWindow.MsecToString(_start); }
        }

        public long End
        {   // returns 0 if not set (so play to end of file)
            get { return _end; }
            set { _end = value; NotifyPropertyChanged("DurationString"); }
        }
        public long Duration
        {
            get { return _end>0? _end - _start : 0; }   // return 0 if _end not set
            set { _end = _start + value; }
        }
        public string DurationString
        {
            get { return _end > 0 ? MainWindow.MsecToString(_end-_start) : ""; }
        }

        public int Count {
            get { return _count;}
            set { _count = value; NotifyPropertyChanged(); }
        }

        public bool Slow {
            get { return _slow; }
            set { _slow = value;}
        }

        public bool FastSeek {
            get { return _fastSeek; }
            set { _fastSeek = value; }
        }

        public bool? Mute
        {
            get { return _mute; }
            set { _mute = value; }
        }

        public bool IsEmpty { get { return _filepath == null; } }

        public event PropertyChangedEventHandler PropertyChanged;

        private void NotifyPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] String propertyName = "") {
            if (PropertyChanged != null) {
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
        }
    }
}
