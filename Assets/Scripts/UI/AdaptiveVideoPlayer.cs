using UnityEngine;
using UnityEngine.Video;
using System;
using System.Collections.Generic;

namespace ProjectPowerSystemsEngineer.Core
{
    [Serializable]
    public struct VideoRatioProfile
    {
        [Tooltip("标签备注，例如 '16:9' 或 '21:9'")]
        public string label;

        [Tooltip("该视频的目标比例参数，例如 X=16, Y=9")]
        public Vector2 targetRatio;

        [Tooltip("对应的视频源文件")]
        public VideoClip videoClip;
    }

    [RequireComponent(typeof(VideoPlayer))]
    [DefaultExecutionOrder(-10)] // 确保在 VideoPlayer 自动播放前，优先执行此脚本替换视频
    public class AdaptiveVideoPlayer : MonoBehaviour
    {
        [Header("视频比例配置方案")]
        [Tooltip("在这里填入你渲染出来的不同比例的视频")]
        public List<VideoRatioProfile> videoProfiles = new List<VideoRatioProfile>();

        [Header("画面填充策略")]
        [Tooltip("如果勾选，画面将等比放大填满全屏（多余部分会被裁剪）；如果取消勾选，画面将保持完整并在不足处留出黑边。")]
        public bool cropToFillScreen = true;

        private VideoPlayer videoPlayer;

        private void Awake()
        {
            videoPlayer = GetComponent<VideoPlayer>();

            if (videoProfiles == null || videoProfiles.Count == 0)
            {
                Debug.LogWarning("[AdaptiveVideoPlayer] 没有配置任何视频方案！");
                return;
            }

            // 1. 设置画面的填充/裁剪模式 (极其优雅的原生方案)
            videoPlayer.aspectRatio = cropToFillScreen ? VideoAspectRatio.FitOutside : VideoAspectRatio.FitInside;

            // 2. 执行核心算法：选择比例最接近的视频文件
            SelectBestFitVideo();
        }

        private void SelectBestFitVideo()
        {
            // 获取当前玩家屏幕的真实比例
            float screenRatio = (float)Screen.width / Screen.height;

            VideoRatioProfile bestProfile = videoProfiles[0];
            float minDifference = float.MaxValue;

            // 遍历寻找差值最小的配置
            foreach (var profile in videoProfiles)
            {
                if (profile.targetRatio.y == 0) continue; // 防止除以0报错

                float profileRatio = profile.targetRatio.x / profile.targetRatio.y;
                float difference = Mathf.Abs(screenRatio - profileRatio);

                // 如果找到了更接近的比例
                if (difference < minDifference)
                {
                    minDifference = difference;
                    bestProfile = profile;
                }
            }

            // 3. 将最匹配的视频替换给 VideoPlayer
            if (bestProfile.videoClip != null)
            {
                videoPlayer.clip = bestProfile.videoClip;
                Debug.Log($"[系统] 当前屏幕比例: {screenRatio:0.00}。已自动选择最接近的视频档案: {bestProfile.label}");
            }
        }
    }
}