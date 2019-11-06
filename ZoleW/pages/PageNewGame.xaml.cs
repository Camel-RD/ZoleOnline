﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace ZoleW
{
    /// <summary>
    /// Interaction logic for PageNewGame.xaml
    /// </summary>
    public partial class PageNewGame : Page
    {
        public PageNewGame()
        {
            InitializeComponent();
        }

        public NewGamePageVM MyVM
        {
            get
            {
                bool b = DesignerProperties.GetIsInDesignMode(this);
                return new NewGamePageVM(b);
            }
        }
    }
}
