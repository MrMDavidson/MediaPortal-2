#region Copyright (C) 2007-2013 Team MediaPortal

/*
    Copyright (C) 2007-2013 Team MediaPortal
    http://www.team-mediaportal.com

    This file is part of MediaPortal 2

    MediaPortal 2 is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    MediaPortal 2 is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with MediaPortal 2. If not, see <http://www.gnu.org/licenses/>.
*/

#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using MediaPortal.Common;
using MediaPortal.Common.Commands;
using MediaPortal.Common.General;
using MediaPortal.Common.Localization;
using MediaPortal.Common.Settings;
using MediaPortal.Plugins.SlimTv.Client.Helpers;
using MediaPortal.Plugins.SlimTv.Client.Messaging;
using MediaPortal.Plugins.SlimTv.Client.Settings;
using MediaPortal.Plugins.SlimTv.Interfaces;
using MediaPortal.Plugins.SlimTv.Interfaces.Items;
using MediaPortal.Plugins.SlimTv.Interfaces.UPnP.Items;
using MediaPortal.UI.Presentation.DataObjects;
using MediaPortal.UI.Presentation.Workflow;

namespace MediaPortal.Plugins.SlimTv.Client.Models
{
  /// <summary>
  /// Model which holds the GUI state for the GUI test state.
  /// </summary>
  public class SlimTvMultiChannelGuideModel : SlimTvGuideModelBase
  {
    public const string MODEL_ID_STR = "5054408D-C2A9-451f-A702-E84AFCD29C10";
    public static readonly Guid MODEL_ID = new Guid(MODEL_ID_STR);

    protected double _visibleHours;
    protected double _bufferHours = 1.5;
    protected double _programWidthFactor = 6;
    protected double _programsStartOffset = 370;

    #region Constructor

    public SlimTvMultiChannelGuideModel()
    {
      _programActionsDialogName = "DialogProgramActionsFull"; // for MultiChannelGuide we need another dialog

      // User defined layout settings.
      var settings = ServiceRegistration.Get<ISettingsManager>().Load<SlimTvClientSettings>();
      _visibleHours = settings.EpgVisibleHours;
    }

    #endregion

    #region Protected fields

    protected AbstractProperty _guideStartTimeProperty = null;

    protected DateTime _bufferStartTime;
    protected DateTime _bufferEndTime;
    protected int _bufferGroupIndex;

    public DateTime GuideEndTime
    {
      get { return GuideStartTime.AddHours(_visibleHours); }
    }

    #endregion

    #region Variables

    private readonly ItemsList _channelList = new ItemsList();
    private IList<IProgram> _groupPrograms;

    #endregion

    #region GUI properties and methods

    /// <summary>
    /// Exposes the list of channels in current group.
    /// </summary>
    public ItemsList ChannelList
    {
      get { return _channelList; }
    }

    public DateTime GuideStartTime
    {
      get { return (DateTime)_guideStartTimeProperty.GetValue(); }
      set { _guideStartTimeProperty.SetValue(value); }
    }

    public AbstractProperty GuideStartTimeProperty
    {
      get { return _guideStartTimeProperty; }
    }

    public void ScrollForward()
    {
      GuideStartTime = GuideStartTime.AddMinutes(30);
      UpdatePrograms();
    }

    public void ScrollBackward()
    {
      GuideStartTime = GuideStartTime.AddMinutes(-30);
      UpdatePrograms();
    }

    #endregion

    #region Members

    #region Inits and Updates

    protected override void InitModel()
    {
      if (!_isInitialized)
      {
        DateTime startDate = DateTime.Now.RoundDateTime(15, DateFormatExtension.RoundingDirection.Down);
        _guideStartTimeProperty = new WProperty(typeof(DateTime), startDate);
      }
      base.InitModel();
    }

    #endregion

    #region Channel, groups and programs

    protected override void UpdateChannels()
    {
      SetGroupName();
      BackgroundUpdateChannels();
    }

    private void BackgroundUpdateChannels()
    {
      base.UpdateChannels();
      _channelList.Clear();
      if (_channels != null)
        foreach (IChannel channel in _channels)
        {
          IChannel localChannel = channel;
          var channelProgramsItem = new ChannelProgramListItem(channel, new ItemsList())
            {
              Command = new MethodDelegateCommand(() => ShowSingleChannelGuide(localChannel))
            };
          UpdateChannelPrograms(channelProgramsItem);
          _channelList.Add(channelProgramsItem);
        }
      _channelList.FireChange();
    }

    private void ShowSingleChannelGuide(IChannel channel)
    {
      if (channel == null)
        return;

      int channelId = channel.ChannelId;
      int groupId = _channelGroups[_webChannelGroupIndex].ChannelGroupId;
      IWorkflowManager workflowManager = ServiceRegistration.Get<IWorkflowManager>();
      NavigationContextConfig navigationContextConfig = new NavigationContextConfig();
      navigationContextConfig.AdditionalContextVariables = new Dictionary<string, object>();
      navigationContextConfig.AdditionalContextVariables[SlimTvClientModel.KEY_CHANNEL_ID] = channelId;
      navigationContextConfig.AdditionalContextVariables[SlimTvClientModel.KEY_GROUP_ID] = groupId;
      workflowManager.NavigatePush(new Guid("A40F05BB-022E-4247-8BEE-16EB3E0B39C5"), navigationContextConfig);
    }

    private ProgramListItem BuildProgramListItem(IProgram program)
    {
      ProgramProperties programProperties = new ProgramProperties();
      IProgram currentProgram = program;
      programProperties.SetProgram(currentProgram);

      ProgramListItem item = new ProgramListItem(programProperties)
        {
          Command = new MethodDelegateCommand(() => ShowProgramActions(currentProgram))
        };
      item.AdditionalProperties["PROGRAM"] = currentProgram;
      return item;
    }

    private PlaceholderListItem NoProgramPlaceholder(IChannel channel, DateTime? startTime, DateTime? endTime)
    {
      ILocalization loc = ServiceRegistration.Get<ILocalization>();
      DateTime today = GuideStartTime.GetDay();
      ProgramProperties programProperties = new ProgramProperties();
      Program placeholderProgram = new Program
                              {
                                ChannelId = channel.ChannelId,
                                Title = loc.ToString("[SlimTvClient.NoProgram]"),
                                StartTime = startTime.HasValue ? startTime.Value : today,
                                EndTime = endTime.HasValue ? endTime.Value : today.AddDays(1)
                              };
      programProperties.SetProgram(placeholderProgram);

      var item = new PlaceholderListItem(programProperties);
      item.AdditionalProperties["PROGRAM"] = placeholderProgram;

      return item;
    }


    protected override void Update()
    {
      if (!_isInitialized)
        return;
      UpdateProgramsState();
    }

    protected override void UpdateCurrentChannel()
    { }

    protected override void UpdatePrograms()
    {
      UpdateProgramsForGroup();
      foreach (ChannelProgramListItem channel in _channelList)
        UpdateChannelPrograms(channel);

      _channelList.FireChange();
      SlimTvClientMessaging.SendSlimTvClientMessage(SlimTvClientMessaging.MessageType.ProgramsChanged);
      UpdateProgramsState();
    }

    protected void UpdateProgramsForGroup()
    {
      if (
        _bufferGroupIndex != _webChannelGroupIndex || /* Group changed */
        _bufferStartTime == DateTime.MinValue || _bufferEndTime == DateTime.MinValue || /* Buffer not set */
        GuideStartTime < _bufferStartTime || GuideStartTime > _bufferEndTime || /* Cache is out of request range */
        GuideEndTime < _bufferStartTime || GuideEndTime > _bufferEndTime
        )
      {
        _bufferGroupIndex = _webChannelGroupIndex;
        _bufferStartTime = GuideStartTime.AddHours(-_bufferHours);
        _bufferEndTime = GuideEndTime.AddHours(_bufferHours);
        IChannelGroup group = _channelGroups[_webChannelGroupIndex];
        _tvHandler.ProgramInfo.GetProgramsGroup(group, _bufferStartTime, _bufferEndTime, out _groupPrograms);
      }
    }

    protected override bool UpdateRecordingStatus(IProgram program, RecordingStatus newStatus)
    {
      bool changed = base.UpdateRecordingStatus(program, newStatus);
      if (changed)
      {
        ChannelProgramListItem programChannel = _channelList.OfType<ChannelProgramListItem>().FirstOrDefault(c => c.Channel.ChannelId == program.ChannelId);
        if (programChannel == null)
          return false;

        ProgramListItem listProgram;
        lock (programChannel.Programs.SyncRoot)
        {
          listProgram = programChannel.Programs.OfType<ProgramListItem>().FirstOrDefault(p => p.Program.ProgramId == program.ProgramId);
          if (listProgram == null)
            return false;
        }
        listProgram.Program.IsScheduled = newStatus != RecordingStatus.None;
      }
      return changed;
    }

    private void UpdateProgramsState()
    {
      lock (_channelList.SyncRoot)
        foreach (ChannelProgramListItem channel in _channelList)
          UpdateChannelProgramsState(channel);
    }

    /// <summary>
    /// Sets the "IsRunning" state of all programs.
    /// </summary>
    /// <param name="channel"></param>
    private static void UpdateChannelProgramsState(ChannelProgramListItem channel)
    {
      lock (channel.Programs.SyncRoot)
        foreach (ProgramListItem program in channel.Programs)
          program.Update();
    }

    private void UpdateChannelPrograms(ChannelProgramListItem channel)
    {
      lock (channel.Programs.SyncRoot)
      {
        channel.Programs.Clear();
        if (_groupPrograms != null)
          _groupPrograms.Where(p => p.ChannelId == channel.Channel.ChannelId && p.StartTime < GuideEndTime).ToList().ForEach(p => channel.Programs.Add(BuildProgramListItem(p)));
        FillNoPrograms(channel, GuideStartTime, GuideEndTime);
      }
      // Don't notify about every channel programs list changes, only for channel list
      // channel.Programs.FireChange();
    }

    private void FillNoPrograms(ChannelProgramListItem channel, DateTime viewPortStart, DateTime viewPortEnd)
    {
      var programs = channel.Programs;
      if (programs.Count == 0)
      {
        programs.Add(NoProgramPlaceholder(channel.Channel, null, null));
        return;
      }
      ProgramListItem firstItem = programs.Cast<ProgramListItem>().First();
      ProgramListItem lastItem = programs.Cast<ProgramListItem>().Last();
      if (firstItem.Program.StartTime > viewPortStart)
        programs.Insert(0, NoProgramPlaceholder(channel.Channel, null, firstItem.Program.StartTime));

      if (lastItem.Program.EndTime < viewPortEnd)
        programs.Add(NoProgramPlaceholder(channel.Channel, lastItem.Program.EndTime, null));
    }

    #endregion

    #endregion

    #region IWorkflowModel implementation

    public override Guid ModelId
    {
      get { return MODEL_ID; }
    }

    public override void EnterModelContext(NavigationContext oldContext, NavigationContext newContext)
    {
      base.EnterModelContext(oldContext, newContext);
      // Init viewport to start with current time.
      GuideStartTime = DateTime.Now.RoundDateTime(15, DateFormatExtension.RoundingDirection.Down);
      _bufferStartTime = _bufferEndTime = DateTime.MinValue;
      UpdatePrograms();
    }

    #endregion
  }
}