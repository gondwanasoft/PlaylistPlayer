using LibVLCSharp.Shared;
using Microsoft.Win32;
using PlaylistPlayer;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;
using Xceed.Wpf.Toolkit;

namespace LibVLCSharp.WPF.Sample
{
    public partial class MainWindow : Window
    {
        String playlistDirectory;
        LibVLC _libVLC;
        MediaPlayer _mediaPlayer;
        public ObservableCollection<Segment> segmentList;
        Segment _currentSegment;                // entry in segmentList
        int segmentIndex = 0;                   // index into segmentList of currently-playing segment
        Boolean fullScreen = true;
        Boolean loop;                          // repeat whole playlist indefinitely
        Boolean slow;                          // apply :rate=0.5 to all segments unless otherwise specified
        Boolean fastSeek;                      // apply :input-fast-seek to all segments unless otherwise specified
        Boolean mute;                           // apply :no-audio to all segments unless otherwise specified
        Boolean playAndExit = false;
        Boolean ended = false;                  // whether playlist has been completely played and we're now waiting
        DispatcherTimer notificationTimer = new DispatcherTimer();
        //Point mouseDragOrigin;

        public MainWindow()
        {
            InitializeComponent();

            notificationTimer.Interval = TimeSpan.FromSeconds(1d);
            notificationTimer.Tick += new EventHandler(ClearNotification);

            segmentList = new ObservableCollection<Segment>();
            /*segmentList = new ObservableCollection<Segment>()
            {
                new Segment(){Filepath="rocky.mp4", Start=11242, End=11672},
                new Segment(){Filepath="747.wmv", Start=11242, End=11672},
                new Segment(){Filepath="dreams.wmv", Start=5000, Duration=5000}
            };*/

            this.DataContext = segmentList;

            //segmentList.Add(new Segment() { Filepath = "jeans.mp4", Start = 20000, Duration = 10000 });

            string[] args = Environment.GetCommandLineArgs();
            if (args.Length == 1) {
                System.Windows.MessageBox.Show("PlaylistPlayer playlist.m3u", "Usage", MessageBoxButton.OK, MessageBoxImage.Information);
                throw new Exception();  // make it go away
            }

            //System.Windows.MessageBox.Show(args[1]);

            ReadPlaylist(Path.GetDirectoryName(args[0]) + "\\PlaylistPlayer.m3u", true);  // read defaults

            SetPlaylist(args[1]);

            videoView.Loaded += VideoView_Loaded;
        }

        private void SetPlaylist(string filename) {
            playlistDirectory = Path.GetDirectoryName(filename);

            Title = Path.GetFileName(filename) + " - Playlist Player";

            ReadPlaylist(filename);  // reads into segmentList

            if (fullScreen && WindowState == System.Windows.WindowState.Normal) ToggleFullScreen(); // TODO 1 only if specified in playlist

            if (segmentList.Count == 0) {
                System.Windows.MessageBox.Show("No media files found.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                throw new Exception();  // make it go away
            }
        }

        private void Open() {
            OpenFileDialog openFileDialog = new OpenFileDialog {
                InitialDirectory = playlistDirectory,
                Filter = "Playlists|*.m3u"
            };
            if (openFileDialog.ShowDialog() != true) return;

            // Reset state to initial:
            segmentList.Clear();
            segmentIndex = 0;
            ended = false;

            SetPlaylist(openFileDialog.FileName);

            LoadNewSegment(segmentList[0]);     // start playing
        }

        void VideoView_Loaded(object sender, RoutedEventArgs e)
        {
            Core.Initialize();

            _libVLC = new LibVLC();
            _mediaPlayer = new MediaPlayer(_libVLC);

            videoView.MediaPlayer = _mediaPlayer;

            _mediaPlayer.EndReached += OnEndReached;

            LoadNewSegment(segmentList[0]);
            /* All this replaced by LoadNewSegment() above:
            _currentSegment = segmentList[0];
            //filename = "747.wmv";*/
            //SetMedia();
            /*Media m = new Media(_libVLC, _currentSegment.Filename, FromType.FromPath);
            //m.AddOption("rate=0.5");          // works, but makes scrubbing cumbersome
            //m.AddOption("start-time=11.0");   // works, but makes scrubbing cumbersome
            //m.AddOption("stop-time=12.0");   // works, but makes scrubbing cumbersome
            _mediaPlayer.Play(m);       // can hang on segment change
            _mediaPlayer.SetPause(true);
            //TimeLabel.Content = _mediaPlayer.Time;
            UpdateTimeLabel(_mediaPlayer.Time);
            //Debug.WriteLine("seekable=" + _mediaPlayer.IsSeekable);
            /*Media m = new Media(_libVLC, filename, FromType.FromPath);
            //m.AddOption("rate=0.5");          // works, but makes scrubbing cumbersome
            //m.AddOption("start-time=11.0");   // works, but makes scrubbing cumbersome
            //m.AddOption("stop-time=12.0");   // works, but makes scrubbing cumbersome
            _mediaPlayer.Play(m);

            //_mediaPlayer.Play(new Media(_libVLC, "jeans.mp4", FromType.FromPath));
            //_mediaPlayer.Play(new Media(_libVLC, "747.wmv", FromType.FromPath));

            _mediaPlayer.SetPause(true);
            //TimeLabel.Content = _mediaPlayer.Time;
            UpdateTimeLabel(_mediaPlayer.Time);
            Debug.WriteLine("seekable=" + _mediaPlayer.IsSeekable);*/
        }

        public static DataGridCell GetCell(DataGrid dataGrid, DataGridRow rowContainer, int column) {
            if (rowContainer != null) {
                DataGridCellsPresenter presenter = FindVisualChild<DataGridCellsPresenter>(rowContainer);
                if (presenter == null) {
                    /* if the row has been virtualized away, call its ApplyTemplate() method
                     * to build its visual tree in order for the DataGridCellsPresenter
                     * and the DataGridCells to be created */
                    rowContainer.ApplyTemplate();
                    presenter = FindVisualChild<DataGridCellsPresenter>(rowContainer);
                }
                if (presenter != null) {
                    if (!(presenter.ItemContainerGenerator.ContainerFromIndex(column) is DataGridCell cell)) {
                        /* bring the column into view
                         * in case it has been virtualized away */
                        dataGrid.ScrollIntoView(rowContainer, dataGrid.Columns[column]);
                        cell = presenter.ItemContainerGenerator.ContainerFromIndex(column) as DataGridCell;
                    }
                    return cell;
                }
            }
            return null;
        }

        public static T FindVisualChild<T>(DependencyObject obj) where T : DependencyObject {
            for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(obj); i++) {
                DependencyObject child = System.Windows.Media.VisualTreeHelper.GetChild(obj, i);
                if (child != null && child is T)
                    return (T)child;
                else {
                    T childOfChild = FindVisualChild<T>(child);
                    if (childOfChild != null)
                        return childOfChild;
                }
            }
            return null;
        }

        /*private void SetMedia()
        {
            // Loads from _currentSegment.
            Media m = new Media(_libVLC, _currentSegment.Filename, FromType.FromPath);
            //m.AddOption("rate=0.5");          // works, but makes scrubbing cumbersome
            //m.AddOption("start-time=11.0");   // works, but makes scrubbing cumbersome
            //m.AddOption("stop-time=12.0");   // works, but makes scrubbing cumbersome
            _mediaPlayer.Play(m);       // can hang on segment change

            //_mediaPlayer.Play(new Media(_libVLC, "jeans.mp4", FromType.FromPath));
            //_mediaPlayer.Play(new Media(_libVLC, "747.wmv", FromType.FromPath));
        }*/

        //public delegate void reloadSegmentDelegateType(MainWindow _this);  // type definition of delegate
        //public delegate void loadNextSegmentDelegateType(MainWindow _this);  // type definition of delegate
        public delegate void mainWindowDelegateType(MainWindow _this);  // type definition of delegate

        static private void ReloadSegment(MainWindow _this) {
            Debug.WriteLine("ReloadSegment()");

            _this.LoadCurrentSegment();

            /*_this._initialTime = _this._selectionStart = (long)_this.Scrubber.SelectionStart;    // Scrubber will be updated in OnLengthChanged
            _this._selectionEnd = (long)_this.Scrubber.SelectionEnd;    // Scrubber will be updated in OnLengthChanged
            //Media m = new Media(_this._libVLC, _this.filename, FromType.FromPath);
            //m.AddOption("rate=0.5");          // works, but makes scrubbing cumbersome
            //m.AddOption("start-time=11.0");   // works, but makes scrubbing cumbersome
            //m.AddOption("stop-time=12.0");   // works, but makes scrubbing cumbersome
            //_this._mediaPlayer.Play(m);
            _this.SetMedia();
            _this._mediaPlayer.SetPause(true);
            _this.DisplayTime(_this._initialTime??0);*/
        }

        mainWindowDelegateType reloadSegmentDelegate = new mainWindowDelegateType(ReloadSegment);
        mainWindowDelegateType loadNextSegmentDelegate = new mainWindowDelegateType(LoadNextSegment);
        mainWindowDelegateType playlistEndedDelegate = new mainWindowDelegateType(PlaylistEnded);

        /*private void OnScrubberFocus(object sender, RoutedEventArgs e)  // user probably clicked on scrubber to change its value
        {
            Debug.WriteLine("OnScrubberFocus()");
            _mediaPlayer.SetPause(true);
        }*/

        /*private void OnForward(object sender, EventArgs e) {  // never seems to get called
            Debug.WriteLine("OnForward(): ");
        }*/

        /*private void OnScrubberDragStarted(object sender, DragStartedEventArgs e)   // user grabs scrubber to drag it
        {
            Debug.WriteLine("OnScrubberDragStarted()");
            _mediaPlayer.SetPause(true);
        }*/

        static public string MsecToString(long time)
        {
            return (new DateTime(time * 10000)).ToString("H:mm:ss.fff");
        }

        private void LoadNewSegment(Segment segment)
        {
            _currentSegment = new Segment(segment);
            LoadCurrentSegment();
            /*//filename = segment.Filename;
            SetMedia();
            _mediaPlayer.SetPause(true);
            _mediaPlayer.Time = segment.Start;
            Debug.WriteLine("LoadNewSegment(): Time={0}", _mediaPlayer.Time);
            DisplayTime(segment.Start);
            _selectionStart = segment.Start;    // Scrubber will be updated in OnLengthChanged
            _selectionEnd = segment.End;    // Scrubber will be updated in OnLengthChanged
            */
        }

        static private void LoadNextSegment(MainWindow _this) {
            if (++_this.segmentIndex >= _this.segmentList.Count) _this.segmentIndex = 0;
            _this.LoadNewSegment(_this.segmentList[_this.segmentIndex]);
        }

        static private void PlaylistEnded(MainWindow _this) {
            if (_this.playAndExit)
                _this.Close();
            else {
                _this._mediaPlayer.Stop();
                _this.ended = true;
            }
        }

        private void LoadCurrentSegment()
        {
            Media media = LoadMedia();
            if (_currentSegment.Start > 0) media.AddOption("start-time=" + _currentSegment.Start / 1000.0);    // works, but is slow in conjunction with SetRate
            if (_currentSegment.End > 0) media.AddOption("stop-time=" + _currentSegment.End / 1000.0);         // works
            if (slow || _currentSegment.Slow) media.AddOption("rate=0.5");         // works, but is slow in conjunction with start-time
            if (_currentSegment.Mute == true || (_currentSegment.Mute == null && mute)) media.AddOption("no-audio");
            if (_currentSegment.Count > 1) media.AddOption("input-repeat=" + (_currentSegment.Count - 1));
            if (fastSeek || _currentSegment.FastSeek) media.AddOption("input-fast-seek");
            //media.AddOption("no-input-fast-seek");
            //_selectionStart = _currentSegment.Start;    // Scrubber will be updated in OnLengthChanged
            //_selectionEnd = _currentSegment.End;    // Scrubber will be updated in OnLengthChanged
            //Media m = new Media(_libVLC, _currentSegment.Filename, FromType.FromPath);
            //m.AddOption("rate=0.5");          // works, but makes scrubbing cumbersome
            /*if (preview)
            {
                if (_currentSegment.Start > 0) m.AddOption("start-time=" + _currentSegment.Start / 1000);   // works, but makes scrubbing cumbersome
                if (_currentSegment.End > 0) m.AddOption("stop-time=" + _currentSegment.End / 1000);   // works, but makes scrubbing cumbersome
                _mediaPlayer.Play(m);
            }*/
            _mediaPlayer.Play(media);       // can hang on segment change
            //_mediaPlayer.SetRate(0.5f);     // works, but is slow in conjunction with start-time
            //_mediaPlayer.SetPause(true);      // use this to pause playback on load
            _mediaPlayer.Time = _currentSegment.Start;  // may be premature or unnecessary
            Debug.WriteLine("LoadCurrentSegment(): Time={0}", _mediaPlayer.Time);
        }

        private Media LoadMedia()
        {
            string filepath = _currentSegment.IsEmpty ? "placeholder.png" : _currentSegment.Filepath;
            Media media = new Media(_libVLC, filepath, FromType.FromPath);
            //media.AddOption("rate=0.5");          // works, but makes UI slow to respond
            //media.StateChanged += OnStateChanged;
            return media;
        }

        /*private void OnStateChanged(object sender, MediaStateChangedEventArgs args) {
            Debug.WriteLine("OnStateChanged(): State={0}", args.State);
        }*/

        private void OnEndReached(object sender, EventArgs e) {
            //Debug.WriteLine("OnEndReached()");

            //this.Dispatcher.BeginInvoke(reloadSegmentDelegate, this);     // works
            if (segmentIndex + 1 >= segmentList.Count && !loop)
                this.Dispatcher.BeginInvoke(playlistEndedDelegate, this);
            else
                this.Dispatcher.BeginInvoke(loadNextSegmentDelegate, this);
        }

        //********************************************************************************** User Input *****

        public void Window_KeyDown(object sender, KeyEventArgs e) {
            Debug.WriteLine("KeyDown={0} {1}", e.Key, e.SystemKey);
            switch (e.Key) {
                case Key.C:
                case Key.Q:
                case Key.Escape:
                    Close();
                    break;
                case Key.P:
                case Key.Space:
                    if (ended)
                        Restart();
                    else
                        _mediaPlayer.Pause();
                    break;
                case Key.A:
                case Key.M:
                case Key.S:
                case Key.V:
                    _mediaPlayer.Mute = !_mediaPlayer.Mute;
                    break;
                case Key.F:
                case Key.Enter:
                    ToggleFullScreen();
                    /*if ((Keyboard.Modifiers & ModifierKeys.Control) > 0)
                        toggleFullScreen();
                    else
                        _mediaPlayer.Pause();*/
                    break;
                case Key.L:
                case Key.R:
                    loop = !loop;
                    //NotificationWindow notification = new PlaylistPlayer.NotificationWindow();
                    //notification.Show(this, loop ? "Looping" : "Not looping");
                    ShowNotification(loop ? "Looping" : "Not looping");
                    break;
                case Key.N:
                case Key.Tab:
                case Key.Back:
                    _mediaPlayer.SetPause(true);
                    WindowState = System.Windows.WindowState.Minimized;
                    break;
                case Key.System:
                    if (e.SystemKey == Key.Return) ToggleFullScreen();
                    break;
                case Key.O:
                    if ((Keyboard.Modifiers & ModifierKeys.Control) > 0)
                        Open();
                    else
                        ToggleTopmost();
                    break;
                case Key.T:
                    ToggleTopmost();
                    break;
                case Key.H:
                case Key.OemQuestion:
                    ShowHelp();
                    break;
            }
        }

        private void ToggleTopmost() {
            Topmost = !Topmost;

            //NotificationWindow notification = new PlaylistPlayer.NotificationWindow();
            //notification.Show(this, Topmost ? "On top" : "Not on top");
            ShowNotification(Topmost ? "On top" : "Not on top");
        }

        private void ToggleFullScreen() {
            WindowState = WindowState == System.Windows.WindowState.Normal ? System.Windows.WindowState.Maximized : System.Windows.WindowState.Normal;
            WindowStyle = WindowState == System.Windows.WindowState.Normal ? WindowStyle.SingleBorderWindow : WindowStyle.None;
        }

        private void ShowNotification(string msg) {
            notificationTimer.Stop();
            notificationLabel.Content = msg;
            notificationGrid.Visibility = Visibility.Visible;
            notificationTimer.Start();
        }

        private void ClearNotification(object sender, EventArgs e) {
            notificationGrid.Visibility = Visibility.Collapsed;
            notificationTimer.Stop();
        }

        private void Restart() {
            LoadNextSegment(this);
        }

        private void ShowHelp() {
            HelpWindow helpWindow = new PlaylistPlayer.HelpWindow();
            helpWindow.Show();
        }

        /*private void Window_MouseDown(object sender, MouseButtonEventArgs e) {
            mouseDragOrigin = e.GetPosition(null);
        }

        private void Window_MouseMove(object sender, MouseEventArgs e) {
            if (e.LeftButton != MouseButtonState.Pressed) return;
            Point newPoint = e.GetPosition(null);
            this.Left += newPoint.X - mouseDragOrigin.X;
            this.Top += newPoint.Y - mouseDragOrigin.Y;
        }*/

        //********************************************************************************** Playlist File Reading *****

        private bool ReadPlaylist(string playlistFilename, bool silent=false) { // TODO 1 check that .. in command line works okay; maybe check PlaylistMaker code
            Debug.WriteLine("ReadPlaylist(): {0}", playlistFilename);
            if (!File.Exists(playlistFilename)) {
                if (!silent) System.Windows.MessageBox.Show("Unable to open \"" + playlistFilename + "\"", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            string directory = Path.GetDirectoryName(playlistFilename);
            string line;
            Segment segment = new Segment();

            using (StreamReader sr = new StreamReader(playlistFilename)) {
                while (sr.Peek() >= 0) {
                    line = sr.ReadLine().ToUpper();
                    if (line == "#EXTM3U" || line == "") continue;
                    if (line.StartsWith("#EXTVLCOPT:")) AddPlaylistVLCOption(line, segment);
                    else if (line.StartsWith("#EXTPLPOPT:")) AddPlaylistPLPOption(line);
                    else if (line[0] == '#') continue;      // other unsupported options; eg, #EXTINF
                    else {
                        AddPlaylistFile(directory, line, segment);
                        segment = new Segment();
                    }
                }
            }
            return true;
        }

        private void AddPlaylistPLPOption(string line) {   // playlist-level options
            line = line.Substring(11);

            if (line == "LOOP") loop = true;
            else if (line == "RATE=0.5") slow = true;
            else if (line == "INPUT-FAST-SEEK") fastSeek = true;
            else if (line == "NO-AUDIO") mute = true;
        }

        private void AddPlaylistVLCOption(string line, Segment segment) {   // segment-level options
            line = line.Substring(11);
            //Debug.WriteLine(line);

            if (line.StartsWith("START-TIME=")) segment.Start = (long)(1000 * Double.Parse(line.Substring(11)));
            else if (line.StartsWith("STOP-TIME=")) segment.End = (long)(1000 * Double.Parse(line.Substring(10)));
            else if (line.StartsWith("INPUT-REPEAT=")) segment.Count = int.Parse(line.Substring(13)) + 1;
            else if (line == "FULLSCREEN") fullScreen = true;
            else if (line == "RATE=1") segment.Slow = false;
            else if (line == "RATE=0.5") segment.Slow = true;
            else if (line == "INPUT-FAST-SEEK") segment.FastSeek = true;
            else if (line == "AUDIO") segment.Mute = false;
            else if (line == "NO-AUDIO") segment.Mute = true;
            else if (line == "LOOP") loop = true;
            else if (line == "NO-LOOP") loop = false;
            else if (line == "VIDEO-ON-TOP") Topmost = true;
            else if (line == "NO-VIDEO-ON-TOP") Topmost = false;
            else if (line == "PLAY-AND-EXIT") playAndExit = true;
            else if (line == "NO-PLAY-AND-EXIT") playAndExit = false;
        }

        private bool AddPlaylistFile(string directory, string filename, Segment segment) {
            // filename: media or embedded playlist file (.m3u[8])
            if (!Path.IsPathRooted(filename)) {
                filename = Path.Combine(directory, filename);   // TODO 5 this is pretty ugly if .m3u contains .. but seems to work
            }

            if (filename.EndsWith(".M3U") || filename.EndsWith(".M3U8")) {
                return ReadPlaylist(filename);     // TODO 4 should check for recursion loops
            } else {    // add a segment
                if (!File.Exists(filename)) {
                    System.Windows.MessageBox.Show("Unable to find \"" + filename + "\"", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return false;
                }
                segment.Filepath = filename;
                segmentList.Add(segment);
                return true;
            }
        }
    }
}
// DLL dependencies: LibVLCSharp.dll, LibVLCSharp.WPF.dll, libvlc
// TODO 1 JOSEPHINE: doesn't seek to start of first (and maybe other) segs if slow
// TODO 5 mouse double-click to toggle full screen (hard coz VLC swallows mouse)
// TODO 5 mouse single-click to toggle pause (or hide) (hard coz VLC swallows mouse)
// TODO 5 32x32 icon