﻿using AgentPathPlanning.SearchAlgorithms;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
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
using System.Windows.Threading;

namespace AgentPathPlanning
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private const long UPDATE_FREQUENCY = 3; // Run the search steps this many milliseconds
        private const long BEST_PATH_UPDATE_FREQUENCY = 200; // Show the best path steps this many milliseconds

        // Q-Learning
        private const long NUMBER_OF_EPISODES = 100; // Number of episodes to run the agent simulation for Q-Learning
        private const long MAX_EPISODE_STEPS = 150; // Maximum number of steps to run a Q-Learning episode

        // Cell size
        private const int CELL_HEIGHT = 60;
        private const int CELL_WIDTH = 60;

        // Cell colors
        private SolidColorBrush BEST_PATH_CELL_COLOR = new SolidColorBrush(Color.FromRgb(123, 184, 112));
        private SolidColorBrush UNOCCUPIED_CELL_BACKGROUND_COLOR = new SolidColorBrush(Color.FromRgb(244, 244, 244));

        private GridWorld gridWorld;
        private Cell startingCell;
        private Cell rewardCell;

        private AStar aStarSearch;
        private QLearning qLearningSearch;

        private DispatcherTimer searchTimer;

        private DispatcherTimer showBestPathTimer;

        private LinkedList<Cell> bestPath;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void Grid_Initialized(object sender, EventArgs e)
        {
        }

        private void LoadGridButton_Click(object sender, RoutedEventArgs e)
        {
            // Create OpenFileDialog 
            OpenFileDialog fileDialog = new OpenFileDialog();

            // Set the filter to CSV
            fileDialog.DefaultExt = ".csv";
            fileDialog.Filter = "CSV Files (*.csv)|*.*";

            Nullable<bool> result = fileDialog.ShowDialog();

            // Get the selected filename
            if (result == true)
            {
                // Setup the grid world
                gridWorld = new GridWorld(grid, GridMapParser.Parse(fileDialog.FileName), CELL_HEIGHT, CELL_WIDTH);

                // Add click events for the rectangles
                foreach (Cell cell in gridWorld.GetCells())
                {
                    cell.GetRectangle().MouseLeftButtonDown += new MouseButtonEventHandler(MoveAgent);
                }

                // Setup the agent
                if (gridWorld.GetAgentStartingPosition() != null && gridWorld.GetAgentStartingPosition().Length == 2)
                {
                    int agentRowIndex = gridWorld.GetAgentStartingPosition()[0], agentColumnIndex = gridWorld.GetAgentStartingPosition()[1];

                    gridWorld.SetAgent(new Agent(grid, CELL_HEIGHT, CELL_WIDTH, agentRowIndex, agentColumnIndex));

                    // Get the starting cell
                    startingCell = gridWorld.GetCells()[agentRowIndex, agentColumnIndex];
                }
                else
                {
                    MessageBox.Show("Error: The agent starting position must be specified in the grid map file with the number 1. Please correct and try again.");
                    return;
                }

                // Setup the reward
                if (gridWorld.GetRewardPosition() != null && gridWorld.GetRewardPosition().Length == 2)
                {
                    int rewardRowIndex = gridWorld.GetRewardPosition()[0], rewardColumnIndex = gridWorld.GetRewardPosition()[1];

                    gridWorld.SetReward(new Reward(grid, CELL_HEIGHT, CELL_WIDTH, rewardRowIndex, rewardColumnIndex));

                    // Get the reward cell
                    rewardCell = gridWorld.GetCells()[rewardRowIndex, rewardColumnIndex];
                }
                else
                {
                    MessageBox.Show("Error: The reward starting position must be specified in the grid map file with the number 2. Please correct and try again.");
                    return;
                }
            }

            // Make the start button active
            StartButton.IsEnabled = true;
            ResetButton.IsEnabled = true;
        }

        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            // Make the buttons inactive
            AStarRadioButton.IsEnabled = false;
            QLearningRadioButton.IsEnabled = false;
            LoadGridButton.IsEnabled = false;
            StartButton.IsEnabled = false;

            searchTimer = new DispatcherTimer();
            searchTimer.Interval = TimeSpan.FromMilliseconds(UPDATE_FREQUENCY);



            if ((bool)AStarRadioButton.IsChecked)
            {
                if (aStarSearch == null)
                {
                    aStarSearch = new AStar(gridWorld, startingCell, rewardCell);
                }

                searchTimer.Tick += new EventHandler(aStarSearch.Run);
                searchTimer.Tick += new EventHandler(UpdateAgentPosition);
            }
            else // Q-Learning is checked
            {
                if (qLearningSearch == null)
                {
                    qLearningSearch = new QLearning(gridWorld, startingCell, rewardCell);
                }

                searchTimer.Tick += new EventHandler(qLearningSearch.Run);
                searchTimer.Tick += new EventHandler(UpdateAgentPosition);
            }


            searchTimer.Start();
        }

        private void Stop()
        {
            if (searchTimer != null)
            {
                searchTimer.Stop();
            }

            if (showBestPathTimer != null)
            {
                showBestPathTimer.Stop();
            }
        }

        public void UpdateAgentPosition(object sender, EventArgs e)
        {
            if ((bool)AStarRadioButton.IsChecked)
            {
                gridWorld.GetAgent().SetRowIndex(aStarSearch.GetCurrentCell().GetRowIndex());
                gridWorld.GetAgent().SetColumnIndex(aStarSearch.GetCurrentCell().GetColumnIndex());

                // Check if agent is on the reward cell
                // If so, process the reward
                if (aStarSearch.GetCurrentCell().GetRowIndex() == gridWorld.GetRewardPosition()[0] &&
                    aStarSearch.GetCurrentCell().GetColumnIndex() == gridWorld.GetRewardPosition()[1])
                {
                    ProcessFoundReward();
                }
            }
            else // Q-Learning is checked
            {
                gridWorld.GetAgent().SetRowIndex(qLearningSearch.GetCurrentCell().GetRowIndex());
                gridWorld.GetAgent().SetColumnIndex(qLearningSearch.GetCurrentCell().GetColumnIndex());

                IlluminateCell();

                // Check if agent is on the reward cell
                // If so, swap the images and stop the timer
                if (qLearningSearch.GetCurrentCell().GetRowIndex() == gridWorld.GetRewardPosition()[0] &&
                    qLearningSearch.GetCurrentCell().GetColumnIndex() == gridWorld.GetRewardPosition()[1])
                {
                    ProcessFoundReward();
                }
            }

            gridWorld.GetAgent().UpdatePosition();
        }

        public void ProcessFoundReward()
        {
            if ((bool)AStarRadioButton.IsChecked)
            {
                // Stop the search
                Stop();

                AStarExport.Save(gridWorld.GetCells());

                bestPath = aStarSearch.GetBestPath();

                showBestPathTimer = new DispatcherTimer();
                showBestPathTimer.Interval = TimeSpan.FromMilliseconds(BEST_PATH_UPDATE_FREQUENCY);
                showBestPathTimer.Tick += new EventHandler(StepThroughBestPath);

                showBestPathTimer.Start();
            }
            else // Q-Learning is checked
            {
                if (qLearningSearch.IsTraining())
                {
                    qLearningSearch.RestartEpisode(null);
                }
                else
                {
                    // Stop the search
                    Stop();

                    toggleAgentImage();

                    QTableExport.Save(qLearningSearch.GetQTable());
                }
            }
        }
        

        public void IlluminateCell()
        {
            if ((bool)AStarRadioButton.IsChecked)
            {
                bestPath.First.Value.GetRectangle().Fill = BEST_PATH_CELL_COLOR;
            }
            else // Q-Learning is checked
            {
                // Only illuminate the current cell after training
                if (!qLearningSearch.IsTraining())
                {
                    qLearningSearch.GetCurrentCell().GetRectangle().Fill = BEST_PATH_CELL_COLOR;
                }
                else // Illuminate the Q-Table
                {
                    foreach (Cell cell in gridWorld.GetCells())
                    {
                        if (qLearningSearch.GetSumQValue(cell) > 0)
                        {
                            SolidColorBrush cellBackground = BEST_PATH_CELL_COLOR;
                            cellBackground.Opacity = (qLearningSearch.GetSumQValue(cell) / qLearningSearch.GetReward()) / 6;
                            if (cellBackground.Opacity < 0.2)
                            {
                                cellBackground.Opacity = 0.2;
                            }
                            cell.GetRectangle().Fill = cellBackground;
                        }
                    }
                }
            }
        }

        public void ResetCellFills()
        {
            foreach (Cell cell in gridWorld.GetCells())
            {
                if (!cell.IsObstacle())
                {
                    cell.GetRectangle().Fill = UNOCCUPIED_CELL_BACKGROUND_COLOR;
                }
            }
        }

        public void StepThroughBestPath(object sender, EventArgs e)
        {
            // Illuminate the best path
            IlluminateCell();

            gridWorld.GetAgent().SetRowIndex(bestPath.First.Value.GetRowIndex());
            gridWorld.GetAgent().SetColumnIndex(bestPath.First.Value.GetColumnIndex());

            gridWorld.GetAgent().UpdatePosition();

            // Check if agent is on the reward cell
            // If so, process the reward
            if (bestPath.First.Value.GetRowIndex() == gridWorld.GetRewardPosition()[0] &&
                bestPath.First.Value.GetColumnIndex() == gridWorld.GetRewardPosition()[1])
            {
                toggleAgentImage();

                showBestPathTimer.Stop();
            }

            bestPath.RemoveFirst();
        }

        public void toggleAgentImage()
        {
            // Change the image of the reward cell
            if (gridWorld.GetAgent().GetAgentImage().Visibility == Visibility.Visible)
            {
                gridWorld.GetAgent().ShowAgentWithReward();
            }
            else
            {
                gridWorld.GetAgent().ShowAgentWithoutReward();
            }

            // Hide the reward image
            if (gridWorld.GetReward().GetRewardImage().Visibility == Visibility.Visible)
            {
                gridWorld.GetReward().HideImage();
            }
            else
            {
                gridWorld.GetReward().ShowImage();
            }
        }

        public void MoveAgent(object sender, MouseButtonEventArgs e)
        {
            if (sender is Rectangle)
            {
                if ((bool)AStarRadioButton.IsChecked)
                {

                }
                else if (qLearningSearch != null && !qLearningSearch.IsTraining()) // Q-Learning is checked; Only illuminate after training
                {
                    ResetCellFills();

                    foreach (Cell cell in gridWorld.GetCells())
                    {
                        if ((Rectangle)sender == cell.GetRectangle())
                        {
                            if (showBestPathTimer != null)
                            {
                                showBestPathTimer.Stop();
                            }

                            toggleAgentImage();

                            qLearningSearch.RestartEpisode(cell);
                            UpdateAgentPosition(null, null);

                            showBestPathTimer = new DispatcherTimer();
                            showBestPathTimer.Interval = TimeSpan.FromMilliseconds(BEST_PATH_UPDATE_FREQUENCY);
                            showBestPathTimer.Tick += new EventHandler(qLearningSearch.Run);
                            showBestPathTimer.Tick += new EventHandler(UpdateAgentPosition);

                            showBestPathTimer.Start();
                            return;
                        }
                    }
                }
            }
        }

        private void ResetButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
