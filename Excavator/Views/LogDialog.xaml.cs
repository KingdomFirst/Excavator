using System.Windows;

namespace Excavator
{
    /// <summary>
    /// Interaction logic for LogDialog.xaml
    /// </summary>
    public partial class LogDialog : Window
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="LogDialog"/> class.
        /// </summary>
        public LogDialog()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Handles the Click event of the btnClose control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="RoutedEventArgs"/> instance containing the event data.</param>
        private void btnClose_Click( object sender, RoutedEventArgs e )
        {
            Close();
        }
    }
}
