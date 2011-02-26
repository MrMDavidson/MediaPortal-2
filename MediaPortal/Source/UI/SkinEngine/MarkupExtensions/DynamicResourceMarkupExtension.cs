#region Copyright (C) 2007-2011 Team MediaPortal

/*
    Copyright (C) 2007-2011 Team MediaPortal
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

// Define DEBUG_DRME to log activities of the DynamicResourceMarkupExtension. Setting that switch will slow down the
// UI significantly but can be used to find problems or bugs related to this markup extension.
//#define DEBUG_DRME

using System.Collections.Generic;
using MediaPortal.Core.General;
using MediaPortal.Utilities.DeepCopy;
using MediaPortal.UI.SkinEngine.Controls.Visuals;
using MediaPortal.UI.SkinEngine.Xaml;
using MediaPortal.UI.SkinEngine.Xaml.Exceptions;
using MediaPortal.UI.SkinEngine.MpfElements;
using MediaPortal.UI.SkinEngine.MpfElements.Resources;
using MediaPortal.UI.SkinEngine.SkinManagement;
using MediaPortal.UI.SkinEngine.Xaml.Interfaces;

namespace MediaPortal.UI.SkinEngine.MarkupExtensions
{
  /// <summary>
  /// Controls the tree type to be used when searching a resource in the DynamicResource
  /// markup extension.
  /// </summary>
  public enum TreeSearchMode
  {
    /// <summary>
    /// The resource will be searched by stepping-up the logical tree until the resource
    /// is found. This is the default setting, which is compliant to the WPF behavior of the
    /// DynamicResource markup extension.
    /// </summary>
    LogicalTree,

    /// <summary>
    /// The resource will be searched by stepping-up the visual tree until the resource is found.
    /// </summary>
    VisualTree,

    /// <summary>
    /// The resource will be searched by first stepping-up the logical tree, and if no logical parent
    /// is available, stepping-up the visual tree. In every parent search the logical parent will
    /// be checked first.
    /// </summary>
    Hybrid
  }

  /// <summary>
  /// Controls how the DynamicResource markup extension will assign the specified resource to
  /// its target property.
  /// </summary>
  public enum AssignmentMode
  {
    /// <summary>
    /// A reference to the specified resource will be copied to the target property. This is the
    /// default setting, which is compliant to the WPF behavior of the DynamicResource markup extension.
    /// </summary>
    Reference,

    /// <summary>
    /// Will assign a copy of the resource to the target property. The logical parent of the resource
    /// will be modified to reference the target object.
    /// </summary>
    Copy
  }

  /// <summary>
  /// Implements the MPF DynamicResource markup extension, with extended functionality.
  /// There are some more properties to finer control the behavior of this class.
  /// <seealso cref="TreeSearchMode"/>
  /// <seealso cref="AssignmentMode"/>
  /// </summary>
  /// <remarks>
  /// This class is realized as a <see cref="IBinding">Binding</see>, because it
  /// has to track changes of the source resource and of the search path to it.
  /// </remarks>
  public class DynamicResourceMarkupExtension: BindingBase
  {
    #region Protected fields

#if DEBUG_DRME
    protected static IDictionary<object, int> _meIDs = new Dictionary<object, int>();
    protected static int _idCounter = 0;
#endif

    protected IList<ResourceDictionary> _attachedResources = new List<ResourceDictionary>(); // To which resources are we attached?
    protected IList<AbstractProperty> _attachedPropertiesList = new List<AbstractProperty>(); // To which properties are we attached?
    protected bool _attachedToSkinResources = false; // Are we attached to skin resources?
    protected AbstractProperty _resourceKeyProperty; // Resource key to resolve
    protected AbstractProperty _treeSearchModeProperty;
    protected AbstractProperty _assignmentModeProperty;

    #endregion

    #region Ctor

    /// <summary>
    /// Default constructor. Property <see cref="ResourceKey"/> has to be
    /// specified before this binding is bound.
    /// </summary>
    public DynamicResourceMarkupExtension()
    {
#if DEBUG_DRME
      _meIDs.Add(this, _idCounter++);
#endif
      Init();
      Attach();
    }

    /// <summary>
    /// Creates a new <see cref="DynamicResourceMarkupExtension"/> with the
    /// specified <paramref name="resourceKey"/>.
    /// </summary>
    /// <param name="resourceKey">Key of the resource this instance should resolve.</param>
    public DynamicResourceMarkupExtension(string resourceKey): this()
    {
      ResourceKey = resourceKey;
    }

    void Init()
    {
      _resourceKeyProperty = new SProperty(typeof(string), null);
      _treeSearchModeProperty = new SProperty(typeof (TreeSearchMode), TreeSearchMode.LogicalTree);
      _assignmentModeProperty = new SProperty(typeof(AssignmentMode), AssignmentMode.Reference);
    }

    void Attach()
    {
      _resourceKeyProperty.Attach(OnPropertyChanged);
      _treeSearchModeProperty.Attach(OnPropertyChanged);
      _assignmentModeProperty.Attach(OnPropertyChanged);
    }

    void Detach()
    {
      _resourceKeyProperty.Detach(OnPropertyChanged);
      _treeSearchModeProperty.Detach(OnPropertyChanged);
      _assignmentModeProperty.Detach(OnPropertyChanged);
    }

    public override void DeepCopy(IDeepCopyable source, ICopyManager copyManager)
    {
      Detach();
      base.DeepCopy(source, copyManager);
      DynamicResourceMarkupExtension drme = (DynamicResourceMarkupExtension) source;
      ResourceKey = drme.ResourceKey;
      TreeSearchMode = drme.TreeSearchMode;
      AssignmentMode = drme.AssignmentMode;
      Attach();
    }

    public override void Dispose()
    {
      Detach();
      ResetEventHandlerAttachments();
      base.Dispose();
    }

    #endregion

    #region Public properties

    public AbstractProperty ResourceKeyProperty
    {
      get { return _resourceKeyProperty; }
    }

    public string ResourceKey
    {
      get { return (string) _resourceKeyProperty.GetValue(); }
      set { _resourceKeyProperty.SetValue(value); }
    }

    public AbstractProperty TreeSearchModeProperty
    {
      get { return _treeSearchModeProperty; }
    }

    /// <summary>
    /// Specifies, which tree should be used for the search of the referenced resource.
    /// </summary>
    public TreeSearchMode TreeSearchMode
    {
      get { return (TreeSearchMode) _treeSearchModeProperty.GetValue(); }
      set { _treeSearchModeProperty.SetValue(value); }
    }

    public AbstractProperty AssignmentModeProperty
    {
      get { return _assignmentModeProperty; }
    }

    /// <summary>
    /// Specifies, if the original resource should be used or copied when assigning to
    /// the target property.
    /// </summary>
    public AssignmentMode AssignmentMode
    {
      get { return (AssignmentMode) _assignmentModeProperty.GetValue(); }
      set { _assignmentModeProperty.SetValue(value); }
    }

    #endregion

    #region Protected methods & properties

    protected void OnPropertyChanged(AbstractProperty property, object oldValue)
    {
      if (_active)
        UpdateTarget();
    }

    protected void OnResourcesChanged(ResourceDictionary changedResources)
    {
      if (_active)
        UpdateTarget();
    }

    protected void OnSkinResourcesChanged(SkinResources newResources)
    {
      if (_active)
        UpdateTarget();
    }

    protected void OnSourcePathChanged(AbstractProperty property, object oldValue)
    {
      if (_active)
        UpdateTarget();
    }

    protected void AttachToResources(ResourceDictionary rd)
    {
      rd.ResourcesChanged += OnResourcesChanged;
      _attachedResources.Add(rd);
    }

    protected void AttachToSkinResources()
    {
      SkinContext.SkinResourcesChanged += OnSkinResourcesChanged;
      _attachedToSkinResources = true;
    }

    protected void AttachToSourcePathProperty(AbstractProperty sourcePathProperty)
    {
      if (sourcePathProperty != null)
      {
        _attachedPropertiesList.Add(sourcePathProperty);
        sourcePathProperty.Attach(OnSourcePathChanged);
      }
    }

    protected void ResetEventHandlerAttachments()
    {
      foreach (ResourceDictionary resources in _attachedResources)
        resources.ResourcesChanged -= OnResourcesChanged;
      _attachedResources.Clear();
      if (_attachedToSkinResources)
      {
        SkinContext.SkinResourcesChanged -= OnSkinResourcesChanged;
        _attachedToSkinResources = false;
      }
      foreach (AbstractProperty property in _attachedPropertiesList)
        property.Detach(OnSourcePathChanged);
      _attachedPropertiesList.Clear();
    }

#if DEBUG_DRME

    protected int GetDebugId()
    {
      return _meIDs[this];
    }

    protected void DebugOutput(string output, params object[] objs)
    {
      System.Diagnostics.Trace.WriteLine(string.Format("DREM #{0} ({1}): {2}", GetDebugId(), ToString(), string.Format(output, objs)));
    }

#endif

    protected void UpdateTarget(object value)
    {
      object assignValue;
      if (AssignmentMode == AssignmentMode.Reference)
        assignValue = value;
      else if (AssignmentMode == AssignmentMode.Copy)
      {
        IEnumerable<IBinding> deferredBindings;
        assignValue = MpfCopyManager.DeepCopyCutLP(value, out deferredBindings);
        if (assignValue is DependencyObject && _targetDataDescriptor.TargetObject is DependencyObject)
          ((DependencyObject) assignValue).LogicalParent = (DependencyObject) _targetDataDescriptor.TargetObject;
        MpfCopyManager.ActivateBindings(deferredBindings);
      }
      else
        throw new XamlBindingException("AssignmentMode value {0} is not implemented", AssignmentMode);
#if DEBUG_DRME
      DebugOutput("Setting target value to '{0}'", assignValue);
#endif
      _contextObject.SetBindingValue(_targetDataDescriptor, TypeConverter.Convert(assignValue, _targetDataDescriptor.DataType));
    }

    /// <summary>
    /// This method does the walk in the visual or logical tree, depending on the value
    /// of the <see cref="TreeSearchMode"/> property.
    /// </summary>
    /// <remarks>
    /// This method attaches change handlers to all relevant properties on the searched path.
    /// </remarks>
    /// <param name="obj">The object to get the parent of.</param>
    /// <param name="parent">The parent which was found navigating the visual or
    /// logical tree.</param>
    /// <returns><c>true</c>, if a valid parent was found. In this case, the
    /// <paramref name="parent"/> parameter references a not-<c>null</c> parent.
    /// <c>false</c>, if no adequate parent was found.</returns>
    protected bool FindParent(DependencyObject obj, out DependencyObject parent)
    {
      parent = null;
      AbstractProperty parentProperty;
      if (TreeSearchMode == TreeSearchMode.LogicalTree)
        parentProperty = obj.LogicalParentProperty;
      else if (TreeSearchMode == TreeSearchMode.VisualTree)
      {
        if (obj is Visual)
          parentProperty = ((Visual) obj).VisualParentProperty;
        else
          return false;
      }
      else if (TreeSearchMode == TreeSearchMode.Hybrid)
      {
        parentProperty = obj.LogicalParentProperty;
        if (parentProperty.GetValue() == null && obj is Visual)
        {
          AttachToSourcePathProperty(parentProperty); // Attach to LP too
          parentProperty = ((Visual) obj).VisualParentProperty;
        }
      }
      else
        throw new XamlBindingException("TreeSearchMode value {0} is not implemented", TreeSearchMode);

      // Attach change handler to parent property
      AttachToSourcePathProperty(parentProperty);
      parent = parentProperty.GetValue() as DependencyObject;
#if DEBUG_DRME
      if (parent == null)
        DebugOutput("FindParent didn't find a parent of object '{0}'", obj);
#endif
      return parent != null;
    }

    /// <summary>
    /// Tries to resolve the resource specified by the <see cref="ResourceKey"/>
    /// property, and updates the binding target property.
    /// </summary>
    /// <remarks>
    /// This method must not be called before the
    /// <see cref="BindingBase.SetTargetDataDescriptor(IDataDescriptor)"/> method was called.
    /// </remarks>
    protected bool UpdateTarget()
    {
      ResetEventHandlerAttachments();
      if (KeepBinding)
      { // This instance should be used rather than the evaluated source value
        if (_targetDataDescriptor != null)
        {
          _contextObject.SetBindingValue(_targetDataDescriptor, this);
#if DEBUG_DRME
          DebugOutput("Assigning this DRME to target data descriptor '{0}'", _targetDataDescriptor);
#endif
        }
        return true;
      }
      DependencyObject current = _contextObject;
      while (current != null)
      {
        ResourceDictionary resources = null;
        if (current is UIElement)
          resources = ((UIElement) current).Resources;
        else if (current is ResourceDictionary)
          resources = (ResourceDictionary) current;
        if (resources != null)
        {
          // Attach change handler to resource dictionary
          AttachToResources(resources);
          if (resources.ContainsKey(ResourceKey))
          {
            UpdateTarget(resources[ResourceKey]);
            return true;
          }
        }
        if (!FindParent(current, out current))
          return false;
      }
      // Attach change handler to skin resources
      AttachToSkinResources();
      object result = SkinContext.SkinResources.FindStyleResource(ResourceKey);
      if (result != null)
      {
        UpdateTarget(result);
        return true;
      }
      return false;
    }

    #endregion

    #region IBinding implementation/overrides

    public override void Activate()
    {
      base.Activate();
      if (ResourceKey == null)
        throw new XamlBindingException("DynamicResource: property 'ResourceKey' must be given");
      UpdateTarget();
    }

    #endregion

    #region Base overrides

    public override string ToString()
    {
      return "{DynamicResource " + ResourceKey + "}";
    }

    #endregion
  }
}
