﻿using AZH_Tankai_Client.Modules.Maze;
using AZH_Tankai_Client.Modules.PowerUp;
using AZH_Tankai_Shared;
using GameView;
using Microsoft.AspNetCore.SignalR.Client;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Forms.Integration;
using System.Windows;
using Point = AZH_Tankai_Shared.Point;
using Size = System.Drawing.Size;
using Graphics = System.Drawing.Graphics;
using Color = System.Drawing.Color;
using Pen = System.Drawing.Pen;
using SolidBrush = System.Drawing.SolidBrush;
using Rectangle = System.Drawing.Rectangle;
using PointF = System.Drawing.PointF;

namespace signalrClient
{
    // TODO: Rename Form1, Codesplitting.
    public partial class Form1 : Form
    {
        readonly IDictionary<string, Button> tanks = new Dictionary<string, Button>();
        readonly List<Point> bullets = new List<Point>();
        readonly List<Point> lasers = new List<Point>();
        readonly List<Point> shrapnels = new List<Point>();
        const int speed = 15;
        string currentUser = null;
        readonly HubConnection connection;
        Window1 window;
        TestDrawer test;
        public Form1()
        {
            InitializeComponent();
            // TestDrawer testDrawer = new TestDrawer(this);
            window = new Window1();
            ElementHost.EnableModelessKeyboardInterop(window);
            window.Show();
            test = new TestDrawer(window.Drawer, this);
            this.KeyPreview = true;

            connection = new HubConnectionBuilder()
              //.WithUrl("https://azh-tanks.azurewebsites.net/ControlHub")
              .WithUrl("https://localhost:44308/ControlHub")
              .Build();

            connection.Closed += async (error) =>
            {
                await Task.Delay(new Random().Next(0, 5) * 1000);
                await connection.StartAsync();
                OutputBox.Text += error.Message + "\n";
            };
        }

        private async void Form1_Load(object sender, EventArgs e)
        {
            OutputBox.Text += "Starting connection..\n";
            await connection.StartAsync();
            OutputBox.Text += "Connection started!\n";

            connection.On<string>("ReceiveUser", (user) =>
            {
                this.BeginInvoke((Action)(() =>
                {
                    CreatePlayer(user);
                }));
            });


            connection.On<string, int, int>("ReceiveCoordinate", (user, x, y) =>
            {
                this.BeginInvoke((Action)(() =>
                {
                    if (!tanks.ContainsKey(user))
                    {
                        CreatePlayer(user);
                    }
                    else
                    {
                        window.Drawer.MoveObject(user, x, y, 200);
                    }
                }));
            });

            connection.On<string>("ReceiveBulletCoordinates", (bulletList) =>
            {
                this.BeginInvoke((Action)(() =>
                {
                    List<Bullet> list = JsonSerializer.Deserialize<List<Bullet>>(bulletList);
                    List<Point> newBulletList = new List<Point>();
                    List<Point> newLaserList = new List<Point>();
                    List<Point> newShrapnelList = new List<Point>();
                    list.ForEach(bullet =>
                    {
                        switch (bullet.Type)
                        {
                            case "Laser":
                                { newLaserList.Add(bullet.Location); }
                                break;
                            case "Shrapnel":
                                { newShrapnelList.Add(bullet.Location); }
                                break;
                            default:
                                { newBulletList.Add(bullet.Location); }
                                break;
                        }
                    });
                    window.Drawer.RemoveObjectsByIdPrefix("Bullet");
                    foreach (Point bullet in newBulletList)
                    {
                        window.Drawer.DrawEllipse(
                            $"BulletX{bullet.X}Y{bullet.Y}",
                            bullet.X,
                            bullet.Y,
                            8,
                            8,
                            15
                        );
                    }
                    foreach (Point laser in newLaserList)
                    {
                        window.Drawer.DrawLine(
                            $"BulletLaserX{laser.X}Y{laser.Y}",
                            laser.X,
                            laser.Y,
                            laser.X + 30,
                            laser.Y,
                            3
                        );
                    }
                    foreach (Point shrapnel in newShrapnelList)
                    {
                        window.Drawer.DrawRectangle(
                            $"BulletShrapnelX{shrapnel.X}Y{shrapnel.Y}",
                            shrapnel.X,
                            shrapnel.Y,
                            3,
                            3,
                            15
                        );
                    }
                    //bullets.Clear();
                    //bullets.AddRange(newBulletList);

                    //lasers.Clear();
                    //lasers.AddRange(newLaserList);

                    //shrapnels.Clear();
                    //shrapnels.AddRange(newShrapnelList);

                    // Invalidate();
                }));
            });

            connection.On<string>("PlayerExists", (user) =>
            {
                this.BeginInvoke((Action)(() =>
                {
                    OutputBox.Text += $"Name {user} is currently taken!\n";
                }));
            });

            connection.On<string>("TerminatePlayer", (user) =>
            {
                this.BeginInvoke((Action)(() =>
                {
                    OutputBox.Text += $"{user} disconnected!\n";
                    Button tank = tanks[user];
                    this.Controls.Remove(tank);
                    tanks.Remove(user);

                }));
            });

            connection.On<string>("ReceiveMaze", (maze) =>
            {
                Graphics graphics = this.CreateGraphics();
                TileDrawer tileDrawer = new TileDrawer(window.Drawer, new System.Drawing.Point(5, 5), new Size(30, 30));
                WallDrawer wallDrawer = new WallDrawer(window.Drawer, new System.Drawing.Point(5, 5), new Size(30, 30));
                List<List<MazeCellDTO>> cells = JsonSerializer.Deserialize<List<List<MazeCellDTO>>>(maze);
                tileDrawer.DrawTiles(cells);
                wallDrawer.DrawWalls(cells);
            });

            connection.On<string>("ReceivePowerUp", (powerUp) =>
            {
                Graphics graphics = this.CreateGraphics();
                PowerUpDrawer powerUpDrawer = new PowerUpDrawer(window.Drawer, new System.Drawing.Point(5, 5), new Size(30, 30), new Size(20, 20));
                powerUpDrawer.DrawPowerUp(JsonSerializer.Deserialize<PowerUpDTO>(powerUp));
            });

            connection.On<string>("PowerUpStrategy", (strategy) =>
            {
                OutputBox.Text += $"PowerUp strategy is {strategy}\n";
            });

        }

        private async void CreatePlayerButton_Click(object sender, EventArgs e)
        {
            currentUser = username.Text;
            await connection.InvokeAsync("SendPlayer", username.Text, connection.ConnectionId);
        }

        private void Form1_KeyDown(object sender, KeyEventArgs e)
        {
            if (currentUser != null)
            {
                HandleMovement(sender, e);
                HandleFiring(sender, e);
            }
        }

        private void HandleMovement(object sender, KeyEventArgs e)
        {
            Button tank = tanks[currentUser];
            int x = tank.Location.X;
            int y = tank.Location.Y;

            if (e.KeyCode == Keys.D || e.KeyCode == Keys.Right)
            {
                x += speed;
            }
            else if (e.KeyCode == Keys.A || e.KeyCode == Keys.Left)
            {
                x -= speed;
            }
            else if (e.KeyCode == Keys.W || e.KeyCode == Keys.Up)
            {
                y -= speed;
            }
            else if (e.KeyCode == Keys.S || e.KeyCode == Keys.Down)
            {
                y += speed;
            }

            if (tank.Location.X != x || tank.Location.Y != y)
            {
                tank.Location = new System.Drawing.Point(x, y);
                OutputBox.Text += $"X:{x}, Y:{y}\n";
                connection.InvokeAsync("SendCoordinate", username.Text, x, y);
            }
        }

        private void HandleFiring(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Space || e.KeyCode == Keys.F)
            {
                Button tank = tanks[currentUser];
                int x = tank.Location.X;
                int y = tank.Location.Y;

                var random = new Random();
                var list = new List<string> { "Basic", "Laser", "Shrapnel" };
                int index = random.Next(list.Count);

                connection.InvokeAsync("SendFireBullet", username.Text, list[index], x, y);
            }
        }

        private void CreatePlayer(string user)
        {
            Button tank = new Button();
            OutputBox.Text += $"{user} joined!\n";
            tank.BackColor = Color.FromArgb(new Random().Next(1, 255), new Random().Next(1, 255), new Random().Next(1, 255));
            tank.Text = user;
            tank.Width = 30;
            tank.Height = 30;
            tank.Location = new System.Drawing.Point(100, 100);
            tank.Enabled = false;
            //this.Controls.Add(tank);
            tanks.Add(user, tank);
            window.Drawer.DrawRectangle(user, 100, 100, 20, 20, 13);
        }

        private void CreateBullet(string user, string type, int x, int y)
        {
            Button bullet = new Button();
            OutputBox.Text += $"{user} Fired a bullet\n";
            bullet.BackColor = Color.Black;
            bullet.Text = user;
            bullet.Width = 10;
            bullet.Height = 10;
            bullet.Location = new System.Drawing.Point(x, y);
            bullet.Enabled = false;
        }

        private async void GenerateMaze_Click(object sender, EventArgs e)
        {
            this.Invalidate();
            await connection.InvokeAsync("CreateMaze");
            await connection.InvokeAsync("StopPowerUpGeneration");
            await connection.InvokeAsync("GeneratePowerUps");
        }

        private void button1_Click(object sender, EventArgs e)
        {
            window = new Window1();
            ElementHost.EnableModelessKeyboardInterop(window);
            window.Show();
            test = new TestDrawer(window.Drawer, this);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            SolidBrush bulletBrush = new SolidBrush(System.Drawing.Color.Black);
            bullets.ForEach((bullet) =>
            {
                e.Graphics.FillEllipse(bulletBrush, new Rectangle(bullet.X, bullet.Y, 8, 8));
            });
            bulletBrush.Dispose();

            Pen laserPen = new Pen(Color.Green);
            laserPen.Width = 2;
            lasers.ForEach((laser) =>
            {
                e.Graphics.DrawLine(laserPen, laser.X, laser.Y, laser.X + 30, laser.Y);
            });
            laserPen.Dispose();

            Pen shrapnelPen = new Pen(Color.BlueViolet);
            shrapnelPen.Width = 2;

            shrapnels.ForEach((shrapnel) =>
            {
                PointF[] points = {
                    new PointF(shrapnel.X, shrapnel.Y - 5f),
                    new PointF(shrapnel.X + 5f, shrapnel.Y),
                    new PointF(shrapnel.X, shrapnel.Y + 5f),
                    new PointF(shrapnel.X - 5f, shrapnel.Y),
                    new PointF(shrapnel.X, shrapnel.Y - 5f)
                };
                e.Graphics.DrawPolygon(shrapnelPen, points);
            });
            shrapnelPen.Dispose();
        }
    }
}
