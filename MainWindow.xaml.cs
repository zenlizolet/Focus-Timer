using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;

namespace StudyTimer
{
    public class TodoItem
    {
        public string? Text { get; set; }
        public bool IsCompleted { get; set; }
    }

    public partial class MainWindow : Window
    {
        private readonly DispatcherTimer _timer;
        private TimeSpan _workDuration = TimeSpan.FromMinutes(50);
        private TimeSpan _breakDuration = TimeSpan.FromMinutes(5);
        
        private bool _isBreakMode = false;    // true = break timer, false = Focus timer
        private TimeSpan _duration = TimeSpan.FromMinutes(50);

        private DateTime? _endTime;           // als running
        private TimeSpan _remainingWhenPaused; // als paused
        
        private ObservableCollection<TodoItem> _todoItems;
        
        // Total focus time tracking
        private TimeSpan _totalFocusTime = TimeSpan.Zero;
        private DateTime? _currentFocusStartTime = null;

        public MainWindow()
        {
            InitializeComponent();

            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(200) // soepelere update
            };
            _timer.Tick += (_, _) => UpdateCountdown();

            UpdateUiFromRemaining(_duration);
            UpdateModeText();
            UpdateTotalFocusTimeDisplay();
            
            // Zorg ervoor dat auto-start pauze standaard aan staat
            AutoStartBreakCheckbox.IsChecked = true;
            
            // Initialize todo list
            _todoItems = new ObservableCollection<TodoItem>
            {
            };
            TodoItemsControl.ItemsSource = _todoItems;
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape || e.Key == Key.F11)
            {
                // Toggle tussen fullscreen en windowed mode
                if (WindowStyle == WindowStyle.None && WindowState == WindowState.Maximized)
                {
                    // Ga naar windowed mode
                    WindowStyle = WindowStyle.SingleBorderWindow;
                    WindowState = WindowState.Normal;
                }
                else
                {
                    // Ga naar fullscreen mode
                    WindowStyle = WindowStyle.None;
                    WindowState = WindowState.Maximized;
                }
            }
        }

        private void Start_Click(object sender, RoutedEventArgs e)
        {
            // Als al running: niets doen
            if (_endTime != null) return;

            // Start vanaf paused remaining, anders vanaf duration
            var startRemaining = _remainingWhenPaused != default ? _remainingWhenPaused : _duration;

            if (startRemaining <= TimeSpan.Zero)
                startRemaining = _duration;

            _endTime = DateTime.Now.Add(startRemaining);
            _remainingWhenPaused = default;
            _timer.Start();
            
            // Start tracking focus time als we in werk mode zijn
            if (!_isBreakMode && _currentFocusStartTime == null)
            {
                _currentFocusStartTime = DateTime.Now;
            }
        }

        private void Pause_Click(object sender, RoutedEventArgs e)
        {
            if (_endTime == null) return;

            // Bewaar remaining en stop
            _remainingWhenPaused = _endTime.Value - DateTime.Now;
            if (_remainingWhenPaused < TimeSpan.Zero) _remainingWhenPaused = TimeSpan.Zero;

            _endTime = null;
            _timer.Stop();
            
            // Stop tracking en tel focus tijd bij elkaar op (alleen in werk mode)
            if (!_isBreakMode && _currentFocusStartTime != null)
            {
                var focusedTime = DateTime.Now - _currentFocusStartTime.Value;
                _totalFocusTime += focusedTime;
                _currentFocusStartTime = null;
                UpdateTotalFocusTimeDisplay();
            }

            UpdateUiFromRemaining(_remainingWhenPaused);
        }

        private void Reset_Click(object sender, RoutedEventArgs e)
        {
            _timer.Stop();
            _endTime = null;
            _remainingWhenPaused = default;
            
            // Stop tracking focus time (maar tel niet bij totaal - reset betekent opgeven)
            if (!_isBreakMode && _currentFocusStartTime != null)
            {
                _currentFocusStartTime = null;
            }

            UpdateUiFromRemaining(_duration);
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            SettingsPopup.IsOpen = !SettingsPopup.IsOpen;
        }

        private void SetWorkTimer_Click(object sender, RoutedEventArgs e)
        {
            if (!TryParseMinutes(WorkMinutesBox.Text, out var minutes))
            {
                ModernDialog.Show("Ongeldig", "Vul een geldig aantal minuten in (bijv. 50).");
                return;
            }

            _workDuration = TimeSpan.FromMinutes(minutes);

            // Als we in Focus mode zijn, update de duration
            if (!_isBreakMode)
            {
                _duration = _workDuration;

                // Reset naar nieuwe duration
                _timer.Stop();
                _endTime = null;
                _remainingWhenPaused = default;

                UpdateUiFromRemaining(_duration);
            }

            SettingsPopup.IsOpen = false;
            ModernDialog.Show("Instellingen", $"Focus timer ingesteld op {minutes} minuten.");
        }

        private void SetBreakTimer_Click(object sender, RoutedEventArgs e)
        {
            if (!TryParseMinutes(BreakMinutesBox.Text, out var minutes))
            {
                ModernDialog.Show("Ongeldig", "Vul een geldig aantal minuten in (bijv. 10).");
                return;
            }

            _breakDuration = TimeSpan.FromMinutes(minutes);

            // Als we in break mode zijn, update de duration
            if (_isBreakMode)
            {
                _duration = _breakDuration;

                // Reset naar nieuwe duration
                _timer.Stop();
                _endTime = null;
                _remainingWhenPaused = default;

                UpdateUiFromRemaining(_duration);
            }

            SettingsPopup.IsOpen = false;
            ModernDialog.Show("Instellingen", $"Pauze timer ingesteld op {minutes} minuten.");
        }

        private void PickWallpaper_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Title = "Kies een wallpaper",
                Filter = "Afbeeldingen|*.png;*.jpg;*.jpeg;*.bmp;*.webp|Alles|*.*"
            };

            if (dlg.ShowDialog() != true) return;

            try
            {
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.UriSource = new Uri(dlg.FileName);
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.EndInit();
                bmp.Freeze();

                BgBrush.ImageSource = bmp;
                ImageBackgroundLayer.Visibility = Visibility.Visible;
                SettingsPopup.IsOpen = false;
            }
            catch (Exception ex)
            {
                ModernDialog.Show("Fout", $"Kon afbeelding niet laden.\n\n{ex.Message}");
            }
        }
        
        private void ResetBackground_Click(object sender, RoutedEventArgs e)
        {
            // Verberg de image layer om terug te gaan naar de standaard gradient
            ImageBackgroundLayer.Visibility = Visibility.Collapsed;
            BgBrush.ImageSource = null;
            SettingsPopup.IsOpen = false;
        }

        private void UpdateModeText()
        {
            ModeText.Text = _isBreakMode ? "Break!" : "Focus!";
        }
        
        private void NewTodoTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                var text = NewTodoTextBox.Text.Trim();
                if (!string.IsNullOrEmpty(text))
                {
                    _todoItems.Add(new TodoItem { Text = text, IsCompleted = false });
                    NewTodoTextBox.Text = "";
                }
            }
        }
        
        private void TodoCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            // De databinding zorgt voor de update, we hoeven hier niets te doen
            // De strikethrough wordt automatisch toegepast via de DataTrigger
        }
        
        private void ClearTodoList_Click(object sender, RoutedEventArgs e)
        {
            // Verwijder alle afgevinkte items
            var completedItems = _todoItems.Where(item => item.IsCompleted).ToList();
            
            if (completedItems.Count == 0)
            {
                ModernDialog.Show("Todo List", "Geen afgevinkte items om te verwijderen.");
                return;
            }
            
            bool confirmed = ModernConfirmDialog.ShowConfirm(
                "Todo List", 
                $"Weet je zeker dat je {completedItems.Count} afgevinkte item(s) wilt verwijderen?");
            
            if (confirmed)
            {
                foreach (var item in completedItems)
                {
                    _todoItems.Remove(item);
                }
            }
        }

        private void UpdateCountdown()
        {
            if (_endTime == null)
            {
                _timer.Stop();
                return;
            }

            var remaining = _endTime.Value - DateTime.Now;
            if (remaining <= TimeSpan.Zero)
            {
                remaining = TimeSpan.Zero;
                _timer.Stop();
                _endTime = null;

                // Custom "klaar" feedback geluidje
                PlayNotificationSound();
                
                // Switch naar break of work mode
                if (_isBreakMode)
                {
                    // break is klaar, terug naar Focus mode
                    _isBreakMode = false;
                    _duration = _workDuration;
                    
                    UpdateModeText();
                    UpdateUiFromRemaining(_duration);
                    
                    ModernDialog.Show("Focus!", "Pauze is voorbij! Tijd om weer aan de slag te gaan.");
                }
                else
                {
                    // Focus is klaar! Tel de focus tijd bij elkaar op
                    if (_currentFocusStartTime != null)
                    {
                        var focusedTime = DateTime.Now - _currentFocusStartTime.Value;
                        _totalFocusTime += focusedTime;
                        _currentFocusStartTime = null;
                        UpdateTotalFocusTimeDisplay();
                    }
                    
                    // Switch naar break
                    _isBreakMode = true;
                    _duration = _breakDuration;
                    
                    // Update UI eerst
                    UpdateModeText();
                    UpdateUiFromRemaining(_duration);
                    
                    // Check of we auto-start moeten doen
                    bool shouldAutoStart = AutoStartBreakCheckbox.IsChecked == true;
                    
                    ModernDialog.Show("Pauze tijd!", $"Goed gedaan! Tijd voor een pauze van {_breakDuration.TotalMinutes} minuten.");
                    
                    // Auto-start de break als checkbox is checked
                    if (shouldAutoStart)
                    {
                        _endTime = DateTime.Now.Add(_duration);
                        _timer.Start();
                    }
                    
                    return;
                }
                
                UpdateModeText();
                UpdateUiFromRemaining(_duration);
                return;
            }

            UpdateUiFromRemaining(remaining);
        }

        private void UpdateUiFromRemaining(TimeSpan remaining)
        {
            // mm:ss, ook boven 60 minuten
            var totalMinutes = (int)Math.Floor(remaining.TotalMinutes);
            var seconds = remaining.Seconds;
            TimerText.Text = $"{totalMinutes:00}:{seconds:00}";
        }
        
        private void UpdateTotalFocusTimeDisplay()
        {
            var hours = (int)_totalFocusTime.TotalHours;
            var minutes = _totalFocusTime.Minutes;
            
            if (hours > 0)
            {
                TotalFocusTimeText.Text = $"Totaal gefocust: {hours}u {minutes}m";
            }
            else
            {
                TotalFocusTimeText.Text = $"Totaal gefocust: {minutes}m";
            }
        }

        private void PlayNotificationSound()
        {
            try
            {
                // Probeer custom sound te spelen - ondersteunt zowel .wav als .mp3
                var wavPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "notification.wav");
                var mp3Path = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "notification.mp3");
                
                if (System.IO.File.Exists(mp3Path))
                {
                    // Gebruik MediaPlayer voor MP3
                    var player = new System.Windows.Media.MediaPlayer();
                    player.Open(new Uri(mp3Path));
                    player.Play();
                }
                else if (System.IO.File.Exists(wavPath))
                {
                    // Gebruik SoundPlayer voor WAV
                    var player = new System.Media.SoundPlayer(wavPath);
                    player.Play();
                }
                else
                {
                    // Fallback naar een andere system sound als custom bestand niet bestaat
                    System.Media.SystemSounds.Asterisk.Play();
                }
            }
            catch
            {
                // Als alles faalt, gebruik standaard geluidje
                System.Media.SystemSounds.Exclamation.Play();
            }
        }

        private static bool TryParseMinutes(string text, out int minutes)
        {
            minutes = 0;
            text = text.Trim();

            // Accepteer "50" of "50,0" (NL) / "50.0"
            if (double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out var m) ||
                double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out m))
            {
                if (m < 1 || m > 240) return false; // sanity range
                minutes = (int)Math.Round(m);
                return true;
            }

            return false;
        }
    }
}
