using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;

namespace Fake64
{
    public partial class Fake64Form : Form
    {
        DoubleBufferControl db;

        public Fake64Form()
        {
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.White;
            this.ClientSize = new System.Drawing.Size(403, 284+100);

            //
            this.db = new DoubleBufferControl
            {
                Location = new Point(0, 0),
                ClientSize = new System.Drawing.Size(403, 284)
            };
            this.Controls.Add(db);
        }

        public void Render()
        {
            this.db.Render();
        }
    }
}
