using Unity.Connect.Share.Editor;
using UnityEngine;

namespace Unity.LEGO.Tutorials
{
    /// <summary>
    /// Contains all the callbacks needed for the Build And Publish tutorial.
    /// </summary>
    [CreateAssetMenu(fileName = "PublishCriteria", menuName = "Tutorials/Microgame/PublishCriteria")]
    class PublishCriteria : ScriptableObject
    {
        static ShareWindow shareWindow;
        public bool IsNotDisplayingFirstTimeInstructions()
        {
            if (!IsWebGLPublisherOpen()) { return false; }
            return (shareWindow.currentTab != ShareWindow.TAB_INTRODUCTION);
        }

        public bool IsUserLoggedIn()
        {
            if (!IsWebGLPublisherOpen()) { return false; }
            return (shareWindow.currentTab != ShareWindow.TAB_NOT_LOGGED_IN);
        }

        public bool IsBuildBeingUploaded()
        {
            if (!IsWebGLPublisherOpen()) { return false; }
            return (shareWindow.Store.state.step == ShareStep.Upload);
        }

        public bool IsBuildPublished()
        {
            if (!IsWebGLPublisherOpen()) { return false; }
            return !string.IsNullOrEmpty(shareWindow.Store.state.url);
        }

        public bool AtLeastOneBuildIsRegistered()
        {
            if (!IsWebGLPublisherOpen()) { return false; }
            return ShareUtils.GetAllBuildsDirectories().Count > 0;
        }

        bool IsWebGLPublisherOpen()
        {
            if (shareWindow) { return true; }
            shareWindow = ShareWindow.FindInstance();
            return false;
        }
    }
}
