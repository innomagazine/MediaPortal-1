/* 
 *	Copyright (C) 2007 Team MediaPortal
 *	http://www.team-mediaportal.com
 *
 *  This Program is free software; you can redistribute it and/or modify
 *  it under the terms of the GNU General Public License as published by
 *  the Free Software Foundation; either version 2, or (at your option)
 *  any later version.
 *   
 *  This Program is distributed in the hope that it will be useful,
 *  but WITHOUT ANY WARRANTY; without even the implied warranty of
 *  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
 *  GNU General Public License for more details.
 *   
 *  You should have received a copy of the GNU General Public License
 *  along with GNU Make; see the file COPYING.  If not, write to
 *  the Free Software Foundation, 675 Mass Ave, Cambridge, MA 02139, USA. 
 *  http://www.gnu.org/copyleft/gpl.html
 *
 */

#region Usings
using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using TvControl;
using TvDatabase;
using TvEngine;
using TvEngine.Interfaces;
using TvLibrary.Log;
using TvLibrary.Interfaces;
#endregion

namespace TvEngine.PowerScheduler
{
  #region Enums
  public enum ShutdownMode
  {
    Suspend = 0,
    Hibernate = 1,
    StayOn = 2
  }
  #endregion

  public class PowerScheduler : MarshalByRefObject, IPowerScheduler
  {
    #region Variables
    IController _controller;
    PowerSchedulerFactory _factory;
    List<IStandbyHandler> _standbyHandlers;
    List<IWakeupHandler> _wakeupHandlers;
    System.Timers.Timer _standbyTimer;
    WaitableTimer _wakeupTimer;
    DateTime _lastIdleTime;
    string _lastStandbyPreventer = String.Empty;
    bool _idle = false;
    bool _standbyAllowed = true;
    #endregion

    #region External power management methods and enumerations

    [Flags]
    private enum ExecutionState : uint
    {
      /// <summary>
      /// Some error.
      /// </summary>
      Error = 0,

      /// <summary>
      /// System is required, do not hibernate.
      /// </summary>
      SystemRequired = 1,

      /// <summary>
      /// Display is required, do not hibernate.
      /// </summary>
      DisplayRequired = 2,

      /// <summary>
      /// User is active, do not hibernate.
      /// </summary>
      UserPresent = 4,

      /// <summary>
      /// Use together with the above options to report a
      /// state until explicitly changed.
      /// </summary>
      Continuous = 0x80000000
    }

    [DllImport("kernel32.dll", EntryPoint = "SetThreadExecutionState")]
    private static extern ExecutionState SetThreadExecutionState(ExecutionState esFlags);
    
    #endregion

    #region Power management wrapper methods

    private void AllowStandby()
    {
      if (_standbyAllowed)
        return;
      lock (this)
      {
        ExecutionState result = SetThreadExecutionState(ExecutionState.Continuous);
        if (result == ExecutionState.Error)
        {
          Log.Error("PowerScheduler: Could not disable standby prevention!");
        }
        else
        {
          Log.Debug("PowerScheduler: Disabled standby prevention");
          _standbyAllowed = true;
        }
      }
    }

    private void PreventStandby()
    {
      if (!_standbyAllowed)
        return;
      lock (this)
      {
        ExecutionState result = SetThreadExecutionState(ExecutionState.SystemRequired | ExecutionState.Continuous);
        if (result == ExecutionState.Error)
        {
          Log.Error("Could not enable standby prevention!");
        }
        else
        {
          Log.Debug("PowerScheduler: Enabled standby prevention");
          _standbyAllowed = false;
        }
      }
    }

    #endregion

    #region Constructor
    public PowerScheduler()
    {
      _standbyHandlers = new List<IStandbyHandler>();
      _wakeupHandlers = new List<IWakeupHandler>();
      _lastIdleTime = DateTime.Now;
      _idle = false;
    }
    #endregion

    #region Public methods

    #region Start/Stop methods
    public void Start(IController controller)
    {
      _controller = controller;

      // register to power events generated by the system
      if (GlobalServiceProvider.Instance.IsRegistered<IPowerEventHandler>())
      {
        GlobalServiceProvider.Instance.Get<IPowerEventHandler>().AddPowerEventHandler(new PowerEventHandler(OnPowerEvent));
      }
      else
      {
        Log.Debug("PowerScheduler.Start: Unable to register power event handler!");
      }

      // Add ourselves to the GlobalServiceProvider
      GlobalServiceProvider.Instance.Add<IPowerScheduler>(this);

      // Create the default set of standby/resume handlers
      _factory = new PowerSchedulerFactory(controller);
      _factory.CreateDefaultSet();

      _wakeupTimer = new WaitableTimer();
      _wakeupTimer.OnTimerExpired += new WaitableTimer.TimerExpiredHandler(OnWakeupTimerExpired);

      // start the timer responsible for entering standby
      _standbyTimer = new System.Timers.Timer();
      _standbyTimer.Interval = 60000;
      _standbyTimer.Elapsed += new System.Timers.ElapsedEventHandler(OnStandbyTimerElapsed);
      TvBusinessLayer layer = new TvBusinessLayer();
      if (Convert.ToBoolean(layer.GetSetting("PowerSchedulerShutdownActive", "false").Value))
        _standbyTimer.Enabled = true;
      else
        _standbyTimer.Enabled = false;

      Log.Info("Powerscheduler: started");
    }

    public void Stop()
    {
      _standbyTimer.Enabled = false;
      _standbyTimer.Dispose();

      _wakeupTimer.SecondsToWait = -1;
      _wakeupTimer.Close();

      // Remove the default set of standby/resume handlers
      _factory.RemoveDefaultSet();

      // Remove ourselves from the GlobalServiceProvider
      GlobalServiceProvider.Instance.Remove<IPowerScheduler>();

      // unregister from power events generated by the system
      GlobalServiceProvider.Instance.Get<IPowerEventHandler>().RemovePowerEventHandler(new PowerEventHandler(OnPowerEvent));

      Log.Info("Powerscheduler: stopped");

    }
    #endregion

    #region IPowerScheduler implementation
    public void Register(IStandbyHandler handler)
    {
      if (!_standbyHandlers.Contains(handler))
        _standbyHandlers.Add(handler);
    }
    public void Register(IWakeupHandler handler)
    {
      if (!_wakeupHandlers.Contains(handler))
        _wakeupHandlers.Add(handler);
    }
    public void Unregister(IStandbyHandler handler)
    {
      if (_standbyHandlers.Contains(handler))
        _standbyHandlers.Remove(handler);
    }
    public void Unregister(IWakeupHandler handler)
    {
      if (_wakeupHandlers.Contains(handler))
        _wakeupHandlers.Remove(handler);
    }
    public bool SuspendSystem(string source, bool force)
    {
      Log.Info("PowerScheduler: Manual system suspend requested by {0}", source);
      return EnterSuspendOrHibernate(force);
    }
    #endregion

    #endregion

    #region Private methods
    private void OnWakeupTimerExpired()
    {
      Log.Debug("PowerScheduler: OnResume");
    }

    private void OnStandbyTimerElapsed(object sender, System.Timers.ElapsedEventArgs e)
    {
      if (SystemIdle)
      {
        if (!_idle)
        {
          Log.Info("PowerScheduler: System changed from busy state to idle state");
          _lastIdleTime = DateTime.Now;
          _idle = true;
        }
        else
        {
          TvBusinessLayer layer = new TvBusinessLayer();
          string idleTimeout = layer.GetSetting("PowerSchedulerIdleTimeout", "5").Value;
          if (_lastIdleTime <= DateTime.Now.AddMinutes(-Int32.Parse(idleTimeout)))
          {
            // Idle timeout expired - suspend/hibernate system
            Log.Info("PowerScheduler: System idle since {0} - initiate suspend/hibernate", _lastIdleTime);
            EnterSuspendOrHibernate();
          }
        }
      }
      else
      {
        if (_idle)
        {
          Log.Info("PowerScheduler: System changed from idle state to busy state");
          _idle = false;
        }
      }
    }

    private bool OnPowerEvent(System.ServiceProcess.PowerBroadcastStatus powerStatus)
    {
      switch (powerStatus)
      {
        case System.ServiceProcess.PowerBroadcastStatus.QuerySuspend:
          Log.Debug("PowerScheduler: System wants to enter standby");
          bool idle = SystemIdle;
          Log.Debug("PowerScheduler: System idle: {0}", idle);
          if (idle)
          {
            SetWakeupTimer();
            _standbyTimer.Enabled = false;
          }
          return idle;
        case System.ServiceProcess.PowerBroadcastStatus.QuerySuspendFailed:
          Log.Debug("PowerScheduler: Entering standby was disallowed (blocked)");
          ResetAndEnableStandbyTimer(); 
          return true;
        case System.ServiceProcess.PowerBroadcastStatus.ResumeAutomatic:
          Log.Debug("PowerScheduler: System has resumed automatically from standby");
          ResetAndEnableStandbyTimer();
          return true;
        case System.ServiceProcess.PowerBroadcastStatus.ResumeCritical:
          Log.Debug("PowerScheduler: System has resumed from standby after a critical suspend");
          ResetAndEnableStandbyTimer();
          return true;
        case System.ServiceProcess.PowerBroadcastStatus.ResumeSuspend:
          Log.Debug("PowerScheduler: System has resumed from standby");
          ResetAndEnableStandbyTimer();
          return true;
      }
      return true;
    }

    private void ResetAndEnableStandbyTimer()
    {
      _lastIdleTime = DateTime.Now;
      _idle = false;
      TvBusinessLayer layer = new TvBusinessLayer();
      if (Convert.ToBoolean(layer.GetSetting("PowerSchedulerShutdownActive", "false").Value))
        _standbyTimer.Enabled = true;
      else
        _standbyTimer.Enabled = false;
    }

    private bool EnterSuspendOrHibernate()
    {
      TvBusinessLayer layer = new TvBusinessLayer();
      bool force = bool.Parse(layer.GetSetting("PowerSchedulerForceShutdown", "false").Value);
      return EnterSuspendOrHibernate(force);
    }

    private bool EnterSuspendOrHibernate(bool force)
    {
      if (!_standbyAllowed)
        return false;
      // determine standby mode
      PowerState state = PowerState.Suspend;
      TvBusinessLayer layer = new TvBusinessLayer();
      int standbyMode = Int32.Parse(layer.GetSetting("PowerSchedulerShutdownMode", "2").Value);
      switch (standbyMode)
      {
        case (int)ShutdownMode.Suspend:
          state = PowerState.Suspend;
          break;
        case (int)ShutdownMode.Hibernate:
          state = PowerState.Hibernate;
          break;
        case (int)ShutdownMode.StayOn:
          Log.Debug("PowerScheduler: Standby requested but system is configured to stay on");
          return false;
        default:
          Log.Error("PowerScheduler: unknown shutdown mode: {0}", standbyMode);
          return false;
      }

      // make sure we set the wakeup/resume timer before entering standby
      SetWakeupTimer();

      // activate standby
      Log.Info("PowerScheduler: entering {0} ; forced: {1}", state, force);
      return Application.SetSuspendState(state, force, false);
    }

    private void SetWakeupTimer()
    {
      TvBusinessLayer layer = new TvBusinessLayer();
      if (Convert.ToBoolean(layer.GetSetting("PowerSchedulerWakeupActive", "false").Value))
      {
        // determine next wakeup time from IWakeupHandlers
        DateTime nextWakeup = NextWakeupTime;
        if (nextWakeup < DateTime.MaxValue && nextWakeup > DateTime.Now)
        {
          TimeSpan delta = nextWakeup.Subtract(DateTime.Now);
          _wakeupTimer.SecondsToWait = delta.TotalSeconds;
          Log.Debug("PowerScheduler: Set wakeup timer to wakeup system in {0} minutes", delta.TotalMinutes);
        }
        else
        {
          Log.Debug("PowerScheduler: No pending events found in the future which should wakeup the system");
          _wakeupTimer.SecondsToWait = -1;
        }
      }
    }
    #endregion

    #region Private properties

    private bool SystemIdle
    {
      get
      {
        foreach (IStandbyHandler handler in _standbyHandlers)
          if (handler.DisAllowShutdown)
          {
            if (!_idle && !_lastStandbyPreventer.Equals(handler.HandlerName))
            {
              _lastStandbyPreventer = handler.HandlerName;
              Log.Debug("PowerScheduler: System declared busy by {0}", handler.HandlerName);
            }
            PreventStandby();
            return false;
          }
        AllowStandby();
        return true;
      }
    }

    private DateTime NextWakeupTime
    {
      get
      {
        DateTime nextWakeupTime = DateTime.MaxValue;
        TvBusinessLayer layer = new TvBusinessLayer();
        int idleTimeout = Int32.Parse(layer.GetSetting("PowerSchedulerIdleTimeout", "5").Value);
        int preWakeupTime = Int32.Parse(layer.GetSetting("PowerSchedulerPreWakeupTime", "60").Value);
        DateTime earliestWakeupTime = _lastIdleTime.AddMinutes(idleTimeout);
        Log.Debug("PowerScheduler: earliest wakeup time: {0}", earliestWakeupTime);
        foreach (IWakeupHandler handler in _wakeupHandlers)
        {
          DateTime nextTime = handler.GetNextWakeupTime(earliestWakeupTime);
          if (nextTime < nextWakeupTime)
          {
            Log.Debug("PowerScheduler: found next wakeup time {0} by {1}", nextTime, handler.HandlerName);
            nextWakeupTime = nextTime;
          }
        }
        nextWakeupTime = nextWakeupTime.AddSeconds(-preWakeupTime);
        Log.Debug("PowerScheduler: next wakeup time: {0}", nextWakeupTime);
        return nextWakeupTime;
      }
    }

    #endregion
  }
}
