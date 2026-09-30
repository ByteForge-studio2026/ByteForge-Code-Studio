// Copyright (c) 2026 ByteForge
using ByteForge.Core;
using ByteForge.Core.Legal;
using Microsoft.UI.Xaml.Controls;

namespace ByteForge.Settings.WinUI.Pages;

/// <summary>关于页：图标、版本、信息、免责声明、用户协议。</summary>
public sealed partial class AboutPage : Page
{
    public AboutPage()
    {
        InitializeComponent();

        AppNameText.Text = AppInfo.Name;
        MetaText.Text = $"{AppInfo.Positioning} · {AppInfo.Version}";
        FeatureText.Text = AppInfo.FeatureSummary;
        TechStackText.Text = AppInfo.TechStack;
        TeamText.Text = AppInfo.Team;
        ChannelText.Text = AppInfo.OfficialChannel;
        LicenseText.Text = $"{AppInfo.License}（{AppInfo.LicenseUrl}） · {AppInfo.Copyright}";

        DisclaimerText.Text =
            "本软件为开源、公益性质的免费软件，按“现状”提供，不附带任何明示或暗示的担保；" +
            "开发者不对使用中可能出现的 Bug、数据丢失或任何直接或间接损失承担责任，亦不提供商业化的技术支持。" +
            $"完整条款详见{AppInfo.AgreementName}。";

        AgreementTextBlock.Text = AgreementText.Text;

        AboutIcon.Source = IconSource.Get(128);
    }
}
