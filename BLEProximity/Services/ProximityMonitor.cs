using BLEProximity.Models;
using Stateless;
using System.IO;

namespace BLEProximity.Services;

public class ProximityMonitor : IProximityMonitor, IDisposable
{
    private readonly StateMachine<ProximityState, ProximityTrigger> _stateMachine;
    private readonly object _lock = new();

    private System.Timers.Timer? _outOfRangeTimer;
    private System.Timers.Timer? _countdownTimer;
    private System.Timers.Timer? _gracePeriodTimer;

    private int _outOfRangeTimeoutSec = 10;
    private int _gracePeriodSec = 5;
    private int _inRangeThreshold = -70;
    private int _outOfRangeThreshold = -75;

    private bool _isStarted;
    private bool _gracePeriodActive;
    private bool _disposed;

    public ProximityState CurrentState
    {
        get
        {
            lock (_lock)
            {
                return _stateMachine.State;
            }
        }
    }

    public event EventHandler<ProximityStateChangedEventArgs>? StateChanged;

    public ProximityMonitor()
    {
        _stateMachine = new StateMachine<ProximityState, ProximityTrigger>(
            ProximityState.InRange, FiringMode.Queued);
        ConfigureStateMachine();
    }

    private void ConfigureStateMachine()
    {
        _stateMachine.Configure(ProximityState.InRange)
            .Permit(ProximityTrigger.RssiDropped, ProximityState.OutOfRangePending);

        _stateMachine.Configure(ProximityState.OutOfRangePending)
            .Permit(ProximityTrigger.RssiRecovered, ProximityState.InRange)
            .Permit(ProximityTrigger.TimeoutExpired, ProximityState.Countdown);

        _stateMachine.Configure(ProximityState.Countdown)
            .Permit(ProximityTrigger.RssiRecovered, ProximityState.Cancelled)
            .Permit(ProximityTrigger.CountdownExpired, ProximityState.Executing);

        _stateMachine.Configure(ProximityState.Cancelled)
            .Permit(ProximityTrigger.RssiRecovered, ProximityState.InRange);

        _stateMachine.Configure(ProximityState.Executing)
            .Permit(ProximityTrigger.CommandCompleted, ProximityState.OutOfRangeLatched);

        _stateMachine.Configure(ProximityState.OutOfRangeLatched)
            .Permit(ProximityTrigger.RssiRecovered, ProximityState.InRange);

        _stateMachine.OnTransitioned(OnTransitioned);
    }

    public void Configure(ProximityConfig config)
    {
        lock (_lock)
        {
            _inRangeThreshold = config.InRangeThreshold;
            _outOfRangeThreshold = config.OutOfRangeThreshold;
            _outOfRangeTimeoutSec = Math.Clamp(config.OutOfRangeTimeoutSec, 5, 60);
            _gracePeriodSec = Math.Clamp(config.GracePeriodSec, 0, 30);
        }
    }

    public void Start()
    {
        lock (_lock)
        {
            if (_isStarted)
            {
                System.Diagnostics.Debug.WriteLine("[ProximityMonitor] Start called but already started");
                return;
            }
            
            System.Diagnostics.Debug.WriteLine($"[ProximityMonitor] Starting monitor with grace period: {_gracePeriodSec}s");
            _isStarted = true;

            if (_gracePeriodSec > 0)
            {
                _gracePeriodActive = true;
                _gracePeriodTimer = new System.Timers.Timer(_gracePeriodSec * 1000);
                _gracePeriodTimer.AutoReset = false;
                _gracePeriodTimer.Elapsed += OnGracePeriodElapsed;
                _gracePeriodTimer.Start();
                System.Diagnostics.Debug.WriteLine($"[ProximityMonitor] Grace period timer started for {_gracePeriodSec}s");
            }
            else
            {
                _gracePeriodActive = false;
                System.Diagnostics.Debug.WriteLine("[ProximityMonitor] No grace period, monitor active immediately");
            }
        }
    }

    public void Stop()
    {
        lock (_lock)
        {
            _isStarted = false;
            _gracePeriodActive = false;
            StopAllTimers();
        }
    }

    public void UpdateRssi(ulong bluetoothAddress, double smoothedRssi)
    {
        lock (_lock)
        {
            if (!_isStarted)
            {
                System.Diagnostics.Debug.WriteLine("[ProximityMonitor] UpdateRssi called but monitor not started");
                return;
            }

            if (_gracePeriodActive)
            {
                System.Diagnostics.Debug.WriteLine("[ProximityMonitor] UpdateRssi called but grace period is active");
                return;
            }

            var currentState = _stateMachine.State;
            Console.WriteLine($"[ProximityMonitor] UpdateRssi: address={bluetoothAddress:X12}, RSSI={smoothedRssi:F1}, state={currentState}, thresholds=({_outOfRangeThreshold}, {_inRangeThreshold})");

            switch (currentState)
            {
                case ProximityState.InRange:
                    if (smoothedRssi < _outOfRangeThreshold)
                    {
                        Console.WriteLine($"[ProximityMonitor] RSSI {smoothedRssi:F1} < {_outOfRangeThreshold}, firing RssiDropped");
                        FireTrigger(ProximityTrigger.RssiDropped);
                    }
                    else
                    {
                        Console.WriteLine($"[ProximityMonitor] RSSI {smoothedRssi:F1} >= {_outOfRangeThreshold}, staying InRange");
                    }
                    break;

                case ProximityState.OutOfRangePending:
                    if (smoothedRssi > _inRangeThreshold)
                    {
                        System.Diagnostics.Debug.WriteLine($"[ProximityMonitor] RSSI {smoothedRssi:F1} > {_inRangeThreshold}, firing RssiRecovered");
                        FireTrigger(ProximityTrigger.RssiRecovered);
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"[ProximityMonitor] RSSI {smoothedRssi:F1} <= {_inRangeThreshold}, staying {currentState}");
                    }
                    break;

                case ProximityState.Countdown:
                    if (smoothedRssi > _inRangeThreshold)
                    {
                        System.Diagnostics.Debug.WriteLine($"[ProximityMonitor] RSSI {smoothedRssi:F1} > {_inRangeThreshold}, firing RssiRecovered");
                        FireTrigger(ProximityTrigger.RssiRecovered);
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"[ProximityMonitor] RSSI {smoothedRssi:F1} <= {_inRangeThreshold}, staying {currentState}");
                    }
                    break;

                case ProximityState.Cancelled:
                    // Cancelled transitions immediately to InRange via RssiRecovered
                    if (smoothedRssi > _inRangeThreshold)
                    {
                        System.Diagnostics.Debug.WriteLine($"[ProximityMonitor] RSSI {smoothedRssi:F1} > {_inRangeThreshold}, firing RssiRecovered");
                        FireTrigger(ProximityTrigger.RssiRecovered);
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"[ProximityMonitor] RSSI {smoothedRssi:F1} <= {_inRangeThreshold}, staying {currentState}");
                    }
                    break;

                case ProximityState.Executing:
                    System.Diagnostics.Debug.WriteLine("[ProximityMonitor] Ignoring RSSI update while executing command");
                    break;

                case ProximityState.OutOfRangeLatched:
                    if (smoothedRssi > _inRangeThreshold)
                    {
                        System.Diagnostics.Debug.WriteLine($"[ProximityMonitor] RSSI {smoothedRssi:F1} > {_inRangeThreshold}, releasing out-of-range latch");
                        FireTrigger(ProximityTrigger.RssiRecovered);
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"[ProximityMonitor] RSSI {smoothedRssi:F1} <= {_inRangeThreshold}, command already triggered; waiting for in-range reset");
                    }
                    break;
            }
        }
    }

    /// <summary>
    /// Signals that the command has completed execution.
    /// Called externally after the command executor finishes.
    /// </summary>
    public void NotifyCommandCompleted()
    {
        lock (_lock)
        {
            System.Diagnostics.Debug.WriteLine($"[ProximityMonitor] NotifyCommandCompleted called, current state: {_stateMachine.State}");
            if (_stateMachine.State == ProximityState.Executing)
            {
                System.Diagnostics.Debug.WriteLine("[ProximityMonitor] Firing CommandCompleted trigger");
                FireTrigger(ProximityTrigger.CommandCompleted);
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[ProximityMonitor] WARNING: NotifyCommandCompleted called but state is {_stateMachine.State}, not Executing");
            }
        }
    }

    private void FireTrigger(ProximityTrigger trigger)
    {
        if (_stateMachine.CanFire(trigger))
        {
            _stateMachine.Fire(trigger);
        }
    }

    private void OnTransitioned(StateMachine<ProximityState, ProximityTrigger>.Transition transition)
    {
        Console.WriteLine($"[ProximityMonitor] State transition: {transition.Source} -> {transition.Destination} (trigger: {transition.Trigger})");
        
        // Handle state entry logic here since OnEntry callbacks aren't working
        switch (transition.Destination)
        {
            case ProximityState.InRange:
                Console.WriteLine("[ProximityMonitor] Entered InRange - stopping all timers");
                StopOutOfRangeTimer();
                StopCountdownTimer();
                break;
                
            case ProximityState.OutOfRangePending:
                Console.WriteLine("[ProximityMonitor] Entered OutOfRangePending - starting timeout timer");
                StartOutOfRangeTimer();
                break;
                
            case ProximityState.Countdown:
                Console.WriteLine("[ProximityMonitor] Entered Countdown - starting countdown timer");
                StopOutOfRangeTimer();
                StartCountdownTimer();
                break;
                
            case ProximityState.Cancelled:
                Console.WriteLine("[ProximityMonitor] Entered Cancelled - stopping countdown timer");
                StopCountdownTimer();
                // Immediately transition to InRange
                FireTrigger(ProximityTrigger.RssiRecovered);
                break;
                
            case ProximityState.Executing:
                Console.WriteLine("[ProximityMonitor] Entered Executing - stopping countdown timer");
                StopCountdownTimer();
                break;

            case ProximityState.OutOfRangeLatched:
                Console.WriteLine("[ProximityMonitor] Entered OutOfRangeLatched - command already triggered; waiting for RSSI recovery");
                StopOutOfRangeTimer();
                StopCountdownTimer();
                break;
        }
        
        StateChanged?.Invoke(this, new ProximityStateChangedEventArgs
        {
            OldState = transition.Source,
            NewState = transition.Destination
        });
    }

    private void OnGracePeriodElapsed(object? sender, System.Timers.ElapsedEventArgs e)
    {
        lock (_lock)
        {
            System.Diagnostics.Debug.WriteLine("[ProximityMonitor] Grace period elapsed, monitor now active");
            _gracePeriodActive = false;
            DisposeTimer(ref _gracePeriodTimer);
        }
    }

    private void OnOutOfRangeTimerElapsed(object? sender, System.Timers.ElapsedEventArgs e)
    {
        lock (_lock)
        {
            Console.WriteLine("[ProximityMonitor] OutOfRange timeout expired, firing TimeoutExpired trigger");
            if (_stateMachine.State == ProximityState.OutOfRangePending)
            {
                Console.WriteLine("[ProximityMonitor] State is OutOfRangePending, firing TimeoutExpired");
                FireTrigger(ProximityTrigger.TimeoutExpired);
            }
            else
            {
                Console.WriteLine($"[ProximityMonitor] WARNING: Timer elapsed but state is {_stateMachine.State}, not OutOfRangePending");
            }
        }
    }

    private void OnCountdownTimerElapsed(object? sender, System.Timers.ElapsedEventArgs e)
    {
        lock (_lock)
        {
            System.Diagnostics.Debug.WriteLine("[ProximityMonitor] Countdown timer elapsed, firing CountdownExpired trigger");
        Console.WriteLine("[ProximityMonitor] Countdown timer elapsed, firing CountdownExpired trigger");
            if (_stateMachine.State == ProximityState.Countdown)
            {
                FireTrigger(ProximityTrigger.CountdownExpired);
            }
        }
    }

    private void StartOutOfRangeTimer()
    {
        Console.WriteLine($"[ProximityMonitor] Starting OutOfRange timer for {_outOfRangeTimeoutSec} seconds");
        StopOutOfRangeTimer();
        _outOfRangeTimer = new System.Timers.Timer(_outOfRangeTimeoutSec * 1000);
        _outOfRangeTimer.AutoReset = false;
        _outOfRangeTimer.Elapsed += OnOutOfRangeTimerElapsed;
        _outOfRangeTimer.Start();
        Console.WriteLine("[ProximityMonitor] OutOfRange timer started");
    }

    private void StopOutOfRangeTimer()
    {
        DisposeTimer(ref _outOfRangeTimer);
    }

    private void StartCountdownTimer()
    {
        StopCountdownTimer();
        _countdownTimer = new System.Timers.Timer(3000); // 3-second countdown
        _countdownTimer.AutoReset = false;
        _countdownTimer.Elapsed += OnCountdownTimerElapsed;
        _countdownTimer.Start();
    }

    private void StopCountdownTimer()
    {
        DisposeTimer(ref _countdownTimer);
    }

    private void StopAllTimers()
    {
        DisposeTimer(ref _outOfRangeTimer);
        DisposeTimer(ref _countdownTimer);
        DisposeTimer(ref _gracePeriodTimer);
    }

    private void DisposeTimer(ref System.Timers.Timer? timer)
    {
        if (timer != null)
        {
            timer.Stop();
            timer.Dispose();
            timer = null;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        StopAllTimers();
    }
}
