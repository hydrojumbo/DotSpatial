// ********************************************************************************************************
// Product Name: DotSpatial.Symbology.Forms.dll
// Description:  The core assembly for the DotSpatial 6.0 distribution.
// ********************************************************************************************************
// The contents of this file are subject to the MIT License (MIT)
// you may not use this file except in compliance with the License. You may obtain a copy of the License at
// http://dotspatial.codeplex.com/license
//
// Software distributed under the License is distributed on an "AS IS" basis, WITHOUT WARRANTY OF
// ANY KIND, either expressed or implied. See the License for the specific language governing rights and
// limitations under the License.
//
// The Original Code is DotSpatial.dll
//
// The Initial Developer of this Original Code is Ted Dunsford. Created 11/20/2008 9:11:11 AM
//
// Contributor(s): (Open source contributors should list themselves and their modifications here).
//
// ********************************************************************************************************

using System;
using System.Data;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using DotSpatial.Serialization;

namespace DotSpatial.Symbology.Forms
{
    /// <summary>
    /// Label Setup form
    /// </summary>
    public partial class LabelSetup : Form
    {
        #region Events

        /// <summary>
        /// Occurs after the Apply button has been pressed
        /// </summary>
        public event EventHandler ChangesApplied;

        #endregion

        #region Private Variables

        private ILabelCategory _activeCategory = new LabelCategory(); // Set fake category to avoid null checks
        private bool _ignoreUpdates;
        private ILabelLayer _layer;
        private ILabelLayer _original;
        private bool isLineLayer;
        #endregion

        #region Constructors

        /// <summary>
        /// Creates a new instance of LabelSetup
        /// </summary>
        public LabelSetup()
        {
            InitializeComponent();

            UpdateFontStyles();

            //Populate the label method drop downs
            foreach (var partMethod in Enum.GetValues(typeof(PartLabelingMethod)))
                cmbLabelParts.Items.Add(partMethod);
            foreach (var lo in Enum.GetValues(typeof(LineOrientation)))
                cmbLineAngle.Items.Add(lo);

            tabs.Selecting += TabsSelecting;
            TabsSelecting(tabs, new TabControlCancelEventArgs(tabs.SelectedTab, tabs.SelectedIndex, false, TabControlAction.Selecting));

            UpdatePreview();
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets or sets the layer to use for defining this dialog
        /// </summary>
        public ILabelLayer Layer
        {
            get { return _layer; }
            set
            {
                _original = value;
                if (_original != null) _layer = value.Copy();
                UpdateLayer();
            }
        }

        #endregion

        #region Methods

        /// <summary>
        /// Shows the help for the selected tab.
        /// </summary>
        private void TabsSelecting(object sender, TabControlCancelEventArgs e)
        {
            if (e.TabPage == tabMembers)
            {
                lblHelp.Visible = true;
                lblHelp.Text = SymbologyFormsMessageStrings.LabelSetup_Help2;
            }
            else if (e.TabPage == tabExpression)
            {
                lblHelp.Visible = true;
                lblHelp.Text = SymbologyFormsMessageStrings.LabelSetup_Help3;
            }
            else if (e.TabPage == tabBasic)
            {
                lblHelp.Visible = true;
                lblHelp.Text = SymbologyFormsMessageStrings.LabelSetup_Help4;
            }
            else if (e.TabPage == tabAdvanced)
            {
                lblHelp.Visible = true;
                lblHelp.Text = SymbologyFormsMessageStrings.LabelSetup_Help5;
            }
            else
            {
                lblHelp.Visible = false;
            }
        }

        /// <summary>
        /// Fires the ChangesApplied event.
        /// </summary>
        protected virtual void OnChangesApplied()
        {
            _activeCategory.Expression = sqlExpression.ExpressionText;

            if (ChangesApplied != null) ChangesApplied(this, EventArgs.Empty);

            if (_original != null)
            {
                _original.CopyProperties(_layer);
                _original.CreateLabels();

                // We have no guarantee that the MapFrame property is set, but redrawing the map is important.
                if (_original.FeatureLayer.MapFrame != null)
                    _original.FeatureLayer.MapFrame.Invalidate();
            }
        }

        private void cmdCancel_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void cmdOK_Click(object sender, EventArgs e)
        {
            if (!sqlExpression.ValidateExpression())
            {
                tabs.SelectedTab = tabExpression;
                this.DialogResult = DialogResult.None;
                return;
            }
            OnChangesApplied();
            Close();
        }

        private void cmdApply_Click(object sender, EventArgs e)
        {
            if (!sqlExpression.ValidateExpression())
            {
                tabs.SelectedTab = tabExpression;
                return;
            }
            OnChangesApplied();
        }

        /// <summary>
        /// Updates any content that visually displays the currently selected characteristics.
        /// </summary>
        public void UpdatePreview()
        {
            if (cmbSize.SelectedItem == null) return;
            var size = float.Parse(cmbSize.SelectedItem.ToString());
            var style = (FontStyle)cmbStyle.SelectedIndex;

            try
            {
                lblPreview.Font = new Font(ffcFamilyName.SelectedFamily, size, style);
                lblPreview.BackColor = chkBackgroundColor.Checked ? cbBackgroundColor.Color : SystemColors.Control;
                lblPreview.ForeColor = cbFontColor.Color;
                lblPreview.Text = "Preview";
                lblPreview.Invalidate();
                ttLabelSetup.SetToolTip(lblPreview, "This shows a preview of the font");
                _activeCategory.Symbolizer.FontFamily = ffcFamilyName.SelectedFamily;
                _activeCategory.Symbolizer.FontSize = size;
                _activeCategory.Symbolizer.FontStyle = style;
                _activeCategory.Symbolizer.FontColor = cbFontColor.Color;
            }
            catch
            {
                lblPreview.Font = new Font("Arial", 20F, FontStyle.Bold);
                lblPreview.Text = "Unsupported!";
                ttLabelSetup.SetToolTip(lblPreview, "The specified combination of font family, style, or size is unsupported");
            }
        }

        /// <summary>
        /// When the layer is updated or configured, this updates the data Table aspects if possible.
        /// </summary>
        public void UpdateLayer()
        {
            UpdateCategories();
            if (lbCategories.Items.Count > 0)
                lbCategories.SelectedIndex = 0;
            cmbPriorityField.Items.Clear();
            cmbPriorityField.Items.Add("FID");
            cmbLabelAngleField.Items.Clear();
            if (_layer == null) return;
            if (_layer.FeatureLayer == null) return;
            if (_layer.FeatureLayer.DataSet == null) return;
            if (_layer.FeatureLayer.DataSet.DataTable == null) return;
            sqlExpression.Table = _layer.FeatureLayer.DataSet.DataTable;
            sqlMembers.Table = _layer.FeatureLayer.DataSet.DataTable;
            foreach (DataColumn column in _layer.FeatureLayer.DataSet.DataTable.Columns)
            {
                cmbPriorityField.Items.Add(column.ColumnName);
                cmbLabelAngleField.Items.Add(column.ColumnName);
            }

            isLineLayer = _layer.FeatureLayer.Symbolizer as ILineSymbolizer != null;
            cmbLineAngle.Visible = isLineLayer;
            rbLineBasedAngle.Visible = isLineLayer;

            cmbLabelingMethod.Items.Clear();
            if (isLineLayer)
            {
                foreach (var method in Enum.GetValues(typeof(LineLabelPlacementMethod)))
                    cmbLabelingMethod.Items.Add(method);
            }
            else
            {
                foreach (var method in Enum.GetValues(typeof(LabelPlacementMethod)))
                    cmbLabelingMethod.Items.Add(method);
            }


            UpdateControls();
        }

        /// <summary>
        /// Updates the controls with the data of the active categories symbolizer.
        /// </summary>
        private void UpdateControls()
        {
            if (_ignoreUpdates) return;
            _ignoreUpdates = true;

            var symb = _activeCategory.Symbolizer;

            cmbPriorityField.SelectedItem = symb.PriorityField;
            chkPreventCollision.Checked = symb.PreventCollisions;
            chkPrioritizeLow.Checked = symb.PrioritizeLowValues;

            // Font color & opacity. Set opacity first.
            sldFontOpacity.Value = symb.FontColor.GetOpacity();
            cbFontColor.Color = symb.FontColor;

            // Background color & opacity. Set opacity first.
            sldBackgroundOpacity.Value = symb.BackColor.GetOpacity();
            cbBackgroundColor.Color = symb.BackColor;
            chkBackgroundColor.Checked = symb.BackColorEnabled;

            // Border color & opacity. Set opacity first.
            chkBorder.Checked = symb.BorderVisible;
            sldBorderOpacity.Value = symb.BorderColor.GetOpacity();
            cbBorderColor.Color = symb.BorderColor;

            cmbSize.SelectedItem = symb.FontSize.ToString(CultureInfo.InvariantCulture);
            cmbAlignment.SelectedIndex = (int)symb.Alignment;
            ffcFamilyName.SelectedFamily = symb.FontFamily;
            cmbStyle.SelectedIndex = (int)symb.FontStyle;
            sqlExpression.ExpressionText = _activeCategory.Expression;
            sqlExpression.AllowEmptyExpression = lbCategories.Items.Count == 1;
            sqlMembers.ExpressionText = _activeCategory.FilterExpression;
            labelAlignmentControl1.Value = symb.Orientation;

            // Shadow options.
            chkShadow.Checked = symb.DropShadowEnabled;
            sliderOpacityShadow.Value = symb.DropShadowColor.GetOpacity();
            colorButtonShadow.Color = symb.DropShadowColor;
            nudShadowOffsetX.Value = (decimal)symb.DropShadowPixelOffset.X;
            nudShadowOffsetY.Value = (decimal)symb.DropShadowPixelOffset.Y;

            nudYOffset.Value = (decimal)symb.OffsetY;
            nudXOffset.Value = (decimal)symb.OffsetX;
            clrHalo.Value = symb.HaloColor;
            chkHalo.Checked = symb.HaloEnabled;

            if (isLineLayer)
                cmbLabelingMethod.SelectedItem = symb.LineLabelPlacementMethod;
            else
                cmbLabelingMethod.SelectedItem = symb.LabelPlacementMethod;
            cmbLabelParts.SelectedItem = symb.PartsLabelingMethod;

            // Label Rotation
            rbCommonAngle.Checked = symb.UseAngle;
            rbCommonAngle_CheckedChanged(rbCommonAngle, EventArgs.Empty);
            nudAngle.Value = (decimal)symb.Angle;
            rbIndividualAngle.Checked = symb.UseLabelAngleField;
            rbIndividualAngle_CheckedChanged(rbIndividualAngle, EventArgs.Empty);
            cmbLabelAngleField.SelectedItem = symb.LabelAngleField;
            rbLineBasedAngle.Checked = symb.UseLineOrientation;
            rbLineBasedAngle_CheckedChanged(rbLineBasedAngle, EventArgs.Empty);
            cmbLineAngle.SelectedItem = symb.LineOrientation;

            // Floating format
            tbFloatingFormat.Text = symb.FloatingFormat;

            //--
            _ignoreUpdates = false;
        }

        #region Categories

        /// <summary>
        /// Updates the category list with the categories of the layers symbology.
        /// </summary>
        private void UpdateCategories()
        {
            lbCategories.SuspendLayout();
            lbCategories.Items.Clear();
            foreach (var cat in _layer.Symbology.Categories)
            {
                lbCategories.Items.Insert(0, cat);
            }
            lbCategories.ResumeLayout();
        }

        /// <summary>
        /// Moves the selected category up.
        /// </summary>
        private void btnCategoryUp_Click(object sender, EventArgs e)
        {
            var cat = (ILabelCategory)lbCategories.SelectedItem;
            _layer.Symbology.Promote(cat);
            UpdateCategories();
            lbCategories.SelectedItem = cat;
        }

        /// <summary>
        /// Moves the selected category down.
        /// </summary>
        private void btnCategoryDown_Click(object sender, EventArgs e)
        {
            var cat = (ILabelCategory)lbCategories.SelectedItem;
            _layer.Symbology.Demote(cat);
            UpdateCategories();
            lbCategories.SelectedItem = cat;
        }

        /// <summary>
        /// Adds a new category.
        /// </summary>
        private void btnAdd_Click(object sender, EventArgs e)
        {
            lbCategories.Items.Insert(0, _layer.Symbology.AddCategory());
            sqlExpression.AllowEmptyExpression = lbCategories.Items.Count == 1;
        }

        /// <summary>
        /// Removes the selected category.
        /// </summary>
        private void btnSubtract_Click(object sender, EventArgs e)
        {
            var cat = (ILabelCategory)lbCategories.SelectedItem;
            if (cat == null) return;

            if (lbCategories.Items.Count == 1)
            {
                MessageBox.Show(this, SymbologyFormsMessageStrings.LabelSetup_OneCategoryNeededErr, SymbologyFormsMessageStrings.LabelSetup_OneCategoryNeededErrCaption, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            _ignoreUpdates = true;
            lbCategories.Items.Remove(cat);
            _layer.Symbology.Categories.Remove(cat);
            if (lbCategories.Items.Count > 0) lbCategories.SelectedIndex = 0;
            _ignoreUpdates = false;

            UpdateControls();
        }

        /// <summary>
        /// Updates the controls with the data of the selected category.
        /// </summary>
        private void lbCategories_SelectedIndexChanged(object sender, EventArgs e)
        {
            _activeCategory = (ILabelCategory)lbCategories.SelectedItem ?? new LabelCategory(); // Set fake category to avoid null checks
            UpdateControls();
        }

        #endregion

        #region BasicProperties
        #region Font

        /// <summary>
        /// Rembemer selected FontColor and update Preview.
        /// </summary>
        private void cbFontColor_ColorChanged(object sender, EventArgs e)
        {
            _activeCategory.Symbolizer.FontColor = cbFontColor.Color;
            UpdatePreview();
        }

        /// <summary>
        /// Shows the preview with the selected font and corrects the style-selection.
        /// </summary>
        private void fontFamilyControl1_SelectedItemChanged(object sender, EventArgs e)
        {
            UpdateFontStyles();
            UpdatePreview();
        }

        /// <summary>
        /// Updates the FontStyles with the styles that exist for the selected font.
        /// </summary>
        private void UpdateFontStyles()
        {
            var ff = ffcFamilyName.GetSelectedFamily();
            cmbStyle.Items.Clear();
            for (var i = 0; i < 15; i++)
            {
                var fs = (FontStyle)i;
                if (ff.IsStyleAvailable(fs))
                {
                    cmbStyle.Items.Add(fs);
                }
            }
            cmbStyle.SelectedItem = cmbStyle.Items.Contains(FontStyle.Regular) ? FontStyle.Regular : cmbStyle.Items[0];
        }

        /// <summary>
        /// Shows the preview with the selected font style.
        /// </summary>
        private void cmbStyle_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdatePreview();
        }

        /// <summary>
        /// Shows the preview with the selected font size.
        /// </summary>
        private void cmbSize_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdatePreview();
        }

        #endregion

        /// <summary>
        /// Remembers whether BackColor is enabled.
        /// </summary>
        private void chkBackgroundColor_CheckedChanged(object sender, EventArgs e)
        {
            _activeCategory.Symbolizer.BackColorEnabled = chkBackgroundColor.Checked;
            UpdateControls();
        }

        /// <summary>
        /// Remembers the BackgroundColor and updates the preview.
        /// </summary>
        private void cbBackgroundColor_ColorChanged(object sender, EventArgs e)
        {
            _activeCategory.Symbolizer.BackColor = cbBackgroundColor.Color;
            if (!_ignoreUpdates) chkBackgroundColor.Checked = true;
            UpdatePreview();
        }

        /// <summary>
        /// Remembers whether border gets used.
        /// </summary>
        private void chkBorder_CheckedChanged(object sender, EventArgs e)
        {
            _activeCategory.Symbolizer.BorderVisible = chkBorder.Checked;
        }

        /// <summary>
        /// Remembers the BorderColor and updates the preview.
        /// </summary>
        private void cbBorderColor_ColorChanged(object sender, EventArgs e)
        {
            _activeCategory.Symbolizer.BorderColor = cbBorderColor.Color;
            if (!_ignoreUpdates)
            {
                _activeCategory.Symbolizer.BorderVisible = true;
                chkBorder.Checked = true;
            }
            UpdatePreview();
        }

        /// <summary>
        /// Remember whether low values get prioritized.
        /// </summary>
        private void chkPrioritizeLow_CheckedChanged(object sender, EventArgs e)
        {
            _activeCategory.Symbolizer.PrioritizeLowValues = chkPrioritizeLow.Checked;
        }

        /// <summary>
        /// Remember selected PriorityField.
        /// </summary>
        private void cmbPriorityField_SelectedIndexChanged(object sender, EventArgs e)
        {
            _activeCategory.Symbolizer.PriorityField = (string)cmbPriorityField.SelectedItem;
        }

        /// <summary>
        /// Remember selected PreventCollision.
        /// </summary>
        private void chkPreventCollision_CheckedChanged(object sender, EventArgs e)
        {
            _activeCategory.Symbolizer.PreventCollisions = chkPreventCollision.Checked;
        }

        #region LabelRotation

        /// <summary>
        /// Remember the selected LabelAngleField.
        /// </summary>
        private void cmbLabelAngleField_SelectedIndexChanged(object sender, EventArgs e)
        {
            _activeCategory.Symbolizer.LabelAngleField = (string)cmbLabelAngleField.SelectedItem;
        }

        /// <summary>
        /// Save the changed angle.
        /// </summary>
        private void nudAngle_ValueChanged(object sender, EventArgs e)
        {
            _activeCategory.Symbolizer.Angle = (double)nudAngle.Value;
        }

        /// <summary>
        /// De-/active NumericUpDown for common angle.
        /// </summary>
        private void rbCommonAngle_CheckedChanged(object sender, EventArgs e)
        {
            _activeCategory.Symbolizer.UseAngle = rbCommonAngle.Checked;
            nudAngle.Enabled = rbCommonAngle.Checked;
        }

        /// <summary>
        /// De-/activate combobox for LabelAngleField.
        /// </summary>
        private void rbIndividualAngle_CheckedChanged(object sender, EventArgs e)
        {
            _activeCategory.Symbolizer.UseLabelAngleField = rbIndividualAngle.Checked;
            cmbLabelAngleField.Enabled = rbIndividualAngle.Checked;
        }

        /// <summary>
        /// De-/activate combobox for linebased angles.
        /// </summary>
        private void rbLineBasedAngle_CheckedChanged(object sender, EventArgs e)
        {
            _activeCategory.Symbolizer.UseLineOrientation = rbLineBasedAngle.Checked;
            cmbLineAngle.Enabled = rbLineBasedAngle.Checked;
        }

        /// <summary>
        /// Remember selected LineOrientation.
        /// </summary>
        private void cmbLineAngle_SelectedIndexChanged(object sender, EventArgs e)
        {
            _activeCategory.Symbolizer.LineOrientation = (LineOrientation)cmbLineAngle.SelectedItem;
        }
        #endregion

        /// <summary>
        /// Remember floatingFormat.
        /// </summary>
        private void tbFloatingFormat_TextChanged(object sender, EventArgs e)
        {
            _activeCategory.Symbolizer.FloatingFormat = tbFloatingFormat.Text;
        }
        #endregion

        #region Advanced Properties

        /// <summary>
        /// Remembers whether shadow is used.
        /// </summary>
        private void chkShadow_CheckedChanged(object sender, EventArgs e)
        {
            _activeCategory.Symbolizer.DropShadowEnabled = chkShadow.Checked;
            gpbUseLabelShadow.Enabled = chkShadow.Checked;
        }

        /// <summary>
        /// Remembers the shadows color.
        /// </summary>
        private void colorButtonShadow_ColorChanged(object sender, EventArgs e)
        {
            if (colorButtonShadow.Color != colorButtonShadow.Color.ToTransparent((float)sliderOpacityShadow.Value))
            {
                colorButtonShadow.Color = colorButtonShadow.Color.ToTransparent((float)sliderOpacityShadow.Value);
                _activeCategory.Symbolizer.DropShadowColor = colorButtonShadow.Color;
            }
        }

        /// <summary>
        /// Remembers the shadows opacity.
        /// </summary>
        private void sliderOpacityShadow_ValueChanged(object sender, EventArgs e)
        {
            colorButtonShadow.Color = colorButtonShadow.Color.ToTransparent((float)sliderOpacityShadow.Value);
            _activeCategory.Symbolizer.DropShadowColor = colorButtonShadow.Color;
        }

        /// <summary>
        /// Remember the Y offset of the labelshadow from the center of the placement point.
        /// </summary>
        private void UpDownShadowOffsetY_ValueChanged(object sender, EventArgs e)
        {
            _activeCategory.Symbolizer.DropShadowPixelOffset = new PointF((float)nudShadowOffsetX.Value, (float)nudShadowOffsetY.Value);
        }

        /// <summary>
        /// Remember the X offset of the labelshadow from the center of the placement point.
        /// </summary>
        private void UpDownShadowOffsetX_ValueChanged(object sender, EventArgs e)
        {
            _activeCategory.Symbolizer.DropShadowPixelOffset = new PointF((float)nudShadowOffsetX.Value, (float)nudShadowOffsetY.Value);
        }

        /// <summary>
        /// Remembers whether halo is used.
        /// </summary>
        private void chkHalo_CheckedChanged(object sender, EventArgs e)
        {
            _activeCategory.Symbolizer.HaloEnabled = chkHalo.Checked;
            clrHalo.Enabled = chkHalo.Checked;
        }

        /// <summary>
        /// Remembers the halo color.
        /// </summary>
        private void clrHalo_SelectedItemChanged(object sender, EventArgs e)
        {
            _activeCategory.Symbolizer.HaloColor = clrHalo.Value;
        }

        /// <summary>
        /// Remember the Y offset of the label from the center of the placement point.
        /// </summary>
        private void nudYOffset_ValueChanged(object sender, EventArgs e)
        {
            _activeCategory.Symbolizer.OffsetY = (float)nudYOffset.Value;
        }

        /// <summary>
        /// Remember the X offset of the label from the center of the placement point.
        /// </summary>
        private void nudXOffset_ValueChanged(object sender, EventArgs e)
        {
            _activeCategory.Symbolizer.OffsetX = (float)nudXOffset.Value;
        }

        /// <summary>
        /// Remembers the position of the label relative to the placement point.
        /// </summary>
        private void labelAlignmentControl1_ValueChanged(object sender, EventArgs e)
        {
            _activeCategory.Symbolizer.Orientation = labelAlignmentControl1.Value;
        }

        /// <summary>
        /// Remembers the alignment of multiline text.
        /// </summary>
        private void cmbAlignment_SelectedIndexChanged(object sender, EventArgs e)
        {
            _activeCategory.Symbolizer.Alignment = (StringAlignment)cmbAlignment.SelectedIndex;
        }

        /// <summary>
        /// Remembers the labeling method.
        /// </summary>
        private void cmbLabelingMethod_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (isLineLayer)
            { _activeCategory.Symbolizer.LineLabelPlacementMethod = (LineLabelPlacementMethod)cmbLabelingMethod.SelectedItem; }
            else { _activeCategory.Symbolizer.LabelPlacementMethod = (LabelPlacementMethod)cmbLabelingMethod.SelectedItem; }

        }


        /// <summary>
        /// Remembers the way parts get labeled.
        /// </summary>
        private void cmbLabelParts_SelectedIndexChanged(object sender, EventArgs e)
        {
            _activeCategory.Symbolizer.PartsLabelingMethod = (PartLabelingMethod)cmbLabelParts.SelectedItem;
        }

        #endregion

        /// <summary>
        /// Remembers the expression that is used to find the members that belong to the active category.
        /// </summary>
        private void sqlMembers_ExpressionTextChanged(object sender, EventArgs e)
        {
            _activeCategory.FilterExpression = sqlMembers.ExpressionText;
        }

        #endregion


    }
}