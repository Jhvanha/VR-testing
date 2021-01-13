using System;
using System.Collections.Generic;

using UnityEditorInternal;
using UnityEngine;

using UnityEditor.XR.Management.Metadata;

namespace UnityEditor.XR.Management
{

    internal interface IXRLoaderOrderManager
    {
        List<XRLoaderInfo> AssignedLoaders { get; }
        List<XRLoaderInfo> UnassignedLoaders { get; }

        void AssignLoader(XRLoaderInfo assignedInfo);
        void UnassignLoader(XRLoaderInfo unassignedInfo);
        void Update();
    }

    internal class XRLoaderOrderUI
    {
        struct LoaderInformation
        {
            public string packageName;
            public string packageId;
            public string loaderName;
            public string loaderType;
            public bool toggled;
            public bool stateChanged;
            public bool disabled;
            public IXRCustomLoaderUI customLoaderUI;
        }

        const string k_AtNoLoaderInstance = "There are no XR plugins applicable to this platform.";
        private List<LoaderInformation> m_LoaderMetadata = null;


        ReorderableList m_OrderedList = null;

        public BuildTargetGroup CurrentBuildTargetGroup { get; set; }

        struct Content
        {
            public static readonly string k_HelpUri = "https://docs.unity3d.com/Packages/com.unity.xr.management@3.0/manual/EndUser.html#:~:text=Select%20the%20XR%20Plugin%20Management,Loader%20for%20your%20plug%2Din.&text=Select%20the%20Install%20button%20from,install%20the%20provider%20you%20need.https://docs.unity3d.com/Packages/com.unity.xr.management@3.0/manual/EndUser.html#:~:text=Select%20the%20XR%20Plugin%20Management,Loader%20for%20your%20plug%2Din.&text=Select%20the%20Install%20button%20from,install%20the%20provider%20you%20need";
            public static readonly GUIContent k_LoaderUITitle = EditorGUIUtility.TrTextContent("Plug-in Providers");

            public static readonly GUIContent k_HelpContent = new GUIContent("",
                EditorGUIUtility.IconContent("_Help@2x").image,
                "Selecting an XR Plug-in Provider installs and loads the corresponding package in your project. You can view and manage these packages in the Package Manager.");
        }

        internal XRLoaderOrderUI()
        {
        }

        void SetDisablesStateOnLoadersFromLoader(LoaderInformation li)
        {
            for (int i = 0; i < m_LoaderMetadata.Count; i++)
            {
                var otherLi = m_LoaderMetadata[i];
                if (otherLi.loaderType == li.loaderType)
                    continue;
                if (li.customLoaderUI != null && Array.IndexOf(li.customLoaderUI.IncompatibleLoaders, otherLi.loaderType) >= 0)
                {
                    if (li.toggled && otherLi.toggled)
                    {
                        otherLi.toggled = false;
                        otherLi.stateChanged = true;
                    }
                    otherLi.disabled = li.toggled;
                    m_LoaderMetadata[i] = otherLi;
                }
            }
        }

        void DrawElementCallback(Rect rect, int index, bool isActive, bool isFocused)
        {
            var li = m_LoaderMetadata[index];

            li.toggled = XRPackageMetadataStore.IsLoaderAssigned(li.loaderType, CurrentBuildTargetGroup);
            bool preToggledState = li.toggled;
            EditorGUI.BeginDisabledGroup(li.disabled);

            if (li.customLoaderUI != null)
            {
                li.customLoaderUI.OnGUI(rect);
                li.toggled = li.customLoaderUI.IsLoaderEnabled;
            }
            else
            {
                li.toggled = EditorGUI.ToggleLeft(rect, li.loaderName, preToggledState);
            }

            li.stateChanged = (li.toggled != preToggledState);
            m_LoaderMetadata[index] = li;
            EditorGUI.EndDisabledGroup();
        }

        float GetElementHeight(int index)
        {
            var li = m_LoaderMetadata[index];
            if (li.customLoaderUI != null)
            {
                li.customLoaderUI.SetRenderedLineHeight(m_OrderedList.elementHeight);
                return li.customLoaderUI.RequiredRenderHeight;
            }
            return m_OrderedList.elementHeight;
        }

        internal bool OnGUI(BuildTargetGroup buildTargetGroup)
        {
            var settings = XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(buildTargetGroup);

            if (buildTargetGroup != CurrentBuildTargetGroup || m_LoaderMetadata == null)
            {
                CurrentBuildTargetGroup = buildTargetGroup;

                if (m_LoaderMetadata == null)
                    m_LoaderMetadata = new List<LoaderInformation>();
                else
                    m_LoaderMetadata.Clear();

                foreach (var pmd in XRPackageMetadataStore.GetLoadersForBuildTarget(buildTargetGroup))
                {
                    var newLi = new LoaderInformation() {
                        packageName = pmd.packageName,
                        packageId = pmd.packageId,
                        loaderName = pmd.loaderName,
                        loaderType = pmd.loaderType,
                        toggled = XRPackageMetadataStore.IsLoaderAssigned(pmd.loaderType, buildTargetGroup),
                        disabled = false,
                        customLoaderUI = XRCustomLoaderUIManager.GetCustomLoaderUI(pmd.loaderType, buildTargetGroup)
                        };

                    if (newLi.customLoaderUI != null)
                    {
                        newLi.customLoaderUI.IsLoaderEnabled = newLi.toggled;
                        newLi.customLoaderUI.ActiveBuildTargetGroup = CurrentBuildTargetGroup;
                    }
                    m_LoaderMetadata.Add(newLi);
                }

                if (settings != null)
                {
                    List<LoaderInformation> loadersWantingToDisableOtherLoaders = new List<LoaderInformation>();

                    LoaderInformation li;
                    for (int i = 0; i < m_LoaderMetadata.Count; i++)
                    {
                        li = m_LoaderMetadata[i];
                        if (XRPackageMetadataStore.IsLoaderAssigned(settings.AssignedSettings, li.loaderType))
                        {
                            li.toggled = true;
                            m_LoaderMetadata[i] = li;

                            if (li.customLoaderUI != null)
                            {
                                loadersWantingToDisableOtherLoaders.Add(li);
                            }
                            break;
                        }
                    }

                    foreach(var loader in loadersWantingToDisableOtherLoaders)
                    {
                        SetDisablesStateOnLoadersFromLoader(loader);
                    }
                }

                m_OrderedList = new ReorderableList(m_LoaderMetadata, typeof(LoaderInformation), false, true, false, false);
                m_OrderedList.drawHeaderCallback = (rect) =>
                {
                    var labelSize = EditorStyles.label.CalcSize(Content.k_LoaderUITitle);
                    var labelRect = new Rect(rect);
                    labelRect.width = labelSize.x;

                    labelSize = EditorStyles.label.CalcSize(Content.k_HelpContent);
                    var imageRect = new Rect(rect);
                    imageRect.xMin = labelRect.xMax + 1;
                    imageRect.width = labelSize.x;

                    EditorGUI.LabelField(labelRect, Content.k_LoaderUITitle, EditorStyles.label);
                    if (GUI.Button(imageRect, Content.k_HelpContent, EditorStyles.label))
                    {
                        System.Diagnostics.Process.Start(Content.k_HelpUri);
                    }
                };
                m_OrderedList.drawElementCallback = (rect, index, isActive, isFocused) => DrawElementCallback(rect, index, isActive, isFocused);
                m_OrderedList.drawElementBackgroundCallback = (rect, index, isActive, isFocused) =>
                {
                    var tex = GUI.skin.label.normal.background;
                    if (tex == null && GUI.skin.label.normal.scaledBackgrounds.Length > 0) tex = GUI.skin.label.normal.scaledBackgrounds[0];
                    if (tex == null) return;

                    GUI.DrawTexture(rect, GUI.skin.label.normal.background);
                };
                m_OrderedList.drawFooterCallback = (rect) =>
                {
                    var status = XRPackageMetadataStore.GetCurrentStatusDisplayText();
                    GUI.Label(rect, EditorGUIUtility.TrTextContent(status), EditorStyles.label);
                };
                m_OrderedList.elementHeightCallback = (index) => GetElementHeight(index);
            }

            if (m_LoaderMetadata == null || m_LoaderMetadata.Count == 0)
            {
                EditorGUILayout.HelpBox(k_AtNoLoaderInstance, MessageType.Info);
            }
            else
            {
                m_OrderedList.DoLayoutList();
                if (settings != null)
                {
                    LoaderInformation li;
                    for (int i = 0; i < m_LoaderMetadata.Count; i++)
                    {
                        li = m_LoaderMetadata[i];
                        if (li.stateChanged && li.customLoaderUI != null)
                                SetDisablesStateOnLoadersFromLoader(li);
                    }

                    for (int i = 0; i < m_LoaderMetadata.Count; i++)
                    {
                        li = m_LoaderMetadata[i];
                        if (li.stateChanged)
                        {
                            if (li.toggled)
                            {
                                XRPackageMetadataStore.InstallPackageAndAssignLoaderForBuildTarget(li.packageId, li.loaderType, buildTargetGroup);
                            }
                            else
                            {
                                XRPackageMetadataStore.RemoveLoader(settings.AssignedSettings, li.loaderType, buildTargetGroup);
                            }
                            li.stateChanged = false;
                            m_LoaderMetadata[i] = li;
                        }
                    }
                }
            }

            return false;
        }
    }
}