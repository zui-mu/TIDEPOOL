**原始资源与来源记录**

sources/ 位于仓库根目录，保存原始资源与来源。通常 Unity 运行资产放在 TidePool/Assets；**JigglePhysics 是本地 UPM 运行依赖例外，不能删除或整体忽略 sources/**。来源与维护边界见 [SPEC.md](SPEC.md)。

- mixamo/：四份用户下载的 FBX 已接收并通过动画验证；保留原始名称和文件，校验值见 download_manifest.json。
- unity-official-tests/：此前用于跨骨架验证的 Unity 官方样例、来源提交及许可证。
- 配置完成的女孩与动画：TidePool/Assets/_Game/Characters/GirlV06/。
- 动画检查场景：TidePool/Assets/_Game/Scenes/GirlV06_MixamoValidation.unity。

- JigglePhysics/：v16.0.1 本地 UPM 源码，MIT；固定归档 SHA256 和已记录的 Unity 6.6 等本地补丁见 [TIDEPOOL-PATCHES.md](JigglePhysics/TIDEPOOL-PATCHES.md)。当前记录没有完整下载 URL，不能声称来源与全部本地差异已审计完毕。JigglePhysics-v16.0.1.zip 保留下载原件；实际集成边界见 [Jiggle Spec](JigglePhysics/SPEC.md)。
