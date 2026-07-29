namespace Artemis
{
    /// <summary>
    /// §9.3 視点切替の共通口。非VR用 <see cref="ViewpointController"/>（カメラを直接動かす）と
    /// VR用 <see cref="VRViewpointRig"/>（XR Origin を動かす）を、同じUIから区別なく扱うために置く。
    /// </summary>
    public interface IViewpointSwitcher
    {
        Viewpoint Current { get; }
        void SetOverview();
        void SetOrionView();
    }
}
