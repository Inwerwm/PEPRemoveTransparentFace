using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using PEPlugin;
using PEPlugin.Pmx;
using PEPExtensions;

namespace RemoveTransparentFace
{
    public class RemoveTransparentFace : PEPluginClass
    {
        public RemoveTransparentFace() : base()
        {
        }

        public override string Name
        {
            get
            {
                return "プラグイン名";
            }
        }

        public override string Version
        {
            get
            {
                return "0.0";
            }
        }

        public override string Description
        {
            get
            {
                return "プラグイン説明";
            }
        }

        public override IPEPluginOption Option
        {
            get
            {
                // boot時実行, プラグインメニューへの登録, メニュー登録名
                return new PEPluginOption(false, true, "プラグイン名");
            }
        }

        public override void Run(IPERunArgs args)
        {
            try
            {

            }
            catch (Exception ex)
            {
                Utility.ShowException(ex);
            }
        }
    }
}
