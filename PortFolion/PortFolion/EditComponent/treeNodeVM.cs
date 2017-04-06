﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Houzkin.Tree;
using System.Windows.Input;
using Houzkin.Architecture;
using PortFolion.Core;
using System.Collections.ObjectModel;
using Livet.Commands;
using Houzkin;
using System.ComponentModel;
using Livet.EventListeners.WeakEvents;
using System.Windows;
using PortFolion.IO;

namespace PortFolion.ViewModels {
	public class _CommonNodeVM : ReadOnlyBindableTreeNode<CommonNode, _CommonNodeVM> {
		protected _CommonNodeVM(CommonNode model) : base(model) {
			//listener = new PropertyChangedWeakEventListener(model, new PropertyChangedEventHandler((o,e)=> { ModelPropertyChanged(o, e); }));
			//ReCalc();
		}
		//IDisposable listener;
		protected override _CommonNodeVM GenerateChild(CommonNode modelChildNode) {
			return _CommonNodeVM.Create(modelChildNode);
		}
		bool isExpand = false;
		public bool IsExpand {
			get { return isExpand; }
			set {
				this.SetProperty(ref isExpand, value);
			}
		}
		public NodePath<string> Path => Model.Path;
		public bool IsModelEquals(CommonNode node) => this.Model == node;
		public ObservableCollection<MenuItemVm> MenuList { get; } = new ObservableCollection<MenuItemVm>();
		/// <summary>再計算</summary>
		public void ReCalcurate() {

			var tgts = this.Levelorder().Skip(1).Reverse()
				.Concat(this.Siblings())
				.Concat(this.Upstream().Skip(1));

			foreach (var n in tgts) n.ReCalc();
			this.Root().RaiseReCalcurated();
		}
		public event Action ReCalcurated;
		private void RaiseReCalcurated() => ReCalcurated?.Invoke();

		void reculcHistories() {
			_currentPositionLine = null;
			InvestmentTotal = CurrentPositionLine.Where(a => 0 < a.Value.InvestmentValue).Sum(a => a.Value.InvestmentValue);
			InvestmentReturnTotal = CurrentPositionLine.Where(a => 0 > a.Value.InvestmentValue).Sum(a => a.Value.InvestmentValue) * -1;
		}
		void reculcRate() {
			var amount = Model.Amount;
			foreach(var c in this.Children) {
				if(amount == 0) {
					c.AmountRate = "0";
				}else {
					var aa = (((double)c.Model.Amount / (double)amount) * 100).ToString("0.#");
					c.AmountRate = aa;
				}
			}
		}
		/// <summary>再計算内容</summary>
		protected virtual void ReCalc() {
			reculcHistories();
			reculcRate();
		}

		Dictionary<DateTime, CommonNode> _currentPositionLine;
		protected Dictionary<DateTime,CommonNode> CurrentPositionLine {
			get {
				if (_currentPositionLine != null) return _currentPositionLine;
				var d = (Model.Root() as TotalRiskFundNode).CurrentDate;
				_currentPositionLine = RootCollection
					.GetNodeLine(Model.Path, d)
					.Where(a=>a.Key <= d)
					.ToDictionary(a => a.Key, a => a.Value);
				return _currentPositionLine;
			}
		}
		public DateTime? CurrentDate => (Model.Root() as TotalRiskFundNode)?.CurrentDate;

		#region DataViewColumn
		long _invTotal;
		public long InvestmentTotal {
			get { return _invTotal; }
			private set {
				if (_invTotal == value) return;
				_invTotal = value;
				OnPropertyChanged();
			}
		}
		long _invReturnTotal;
		public long InvestmentReturnTotal {
			get { return _invReturnTotal; }
			private set {
				if (_invReturnTotal == value) return;
				_invReturnTotal = value;
				OnPropertyChanged();
			}
		}
		string amountRate = "-";
		public string AmountRate {
			get { return amountRate; }
			private set {
				if (amountRate == value) return;
				amountRate = value;
				OnPropertyChanged();
			}
		}

		#endregion
		protected override void Dispose(bool disposing) {
			//if (disposing) listener?.Dispose();
			base.Dispose(disposing);
		}
		public static _CommonNodeVM Create(CommonNode node) {
			if (node == null) return null;
			var nt = node.GetNodeType();
			if(nt == NodeType.OtherProduct || nt == NodeType.Stock || nt == NodeType.Forex) {
				return new _FinancialProductVM(node as FinancialProduct);
			}else if(nt == NodeType.Cash) {
				return new _FinancialValueVM(node as FinancialValue);
			}else {
				return new _FinancialBasketVM(node);
			}
		}
	}
	public class MenuItemVm {
		public string Header { get; set; }
		public MenuItemVm() : this(() => { }) { }
		public MenuItemVm(ICommand command) {
			menuCommand = command;
		}
		public MenuItemVm(Action execute) : this(execute,()=>true) { }
		public MenuItemVm(Action execute,Func<bool> canExecute) {
			menuCommand = new ViewModelCommand(execute, canExecute);
		}
		ICommand menuCommand;
		public ICommand MenuCommand => menuCommand;
		ObservableCollection<MenuItemVm> children;

		public ObservableCollection<MenuItemVm> Children 
			=> children = children ?? new ObservableCollection<MenuItemVm>();
	}
	public class _FinancialBasketVM : _CommonNodeVM {
		public _FinancialBasketVM(CommonNode model) : base(model){
			var ty = model.GetNodeType();
			if(ty == NodeType.Account) {
				var vc = new ViewModelCommand(() => {
					var vm = new AccountEditVM(model as AccountNode);
					var w = new Views.AccountEditWindow();
					w.DataContext = vm;
					var r = w.ShowDialog();
					if (vm.EdittingList.Any()) {
						//save or not
						if (r == true) {
							HistoryIO.SaveRoots(vm.EdittingList.Min(), vm.EdittingList.Max());
						} else {
							RootCollection.Instance.Refresh();
						}
						this.ReCalcurate();
					}
				});
				MenuList.Add(new MenuItemVm(vc) { Header = "編集" });
			}else if (ty == NodeType.Broker) {
				var vc = new ViewModelCommand(() => {
					var vm = new NodeNameEditerVM(model, new AccountNode(AccountClass.General));
					var w = new Views.NodeNameEditWindow();
					w.DataContext = vm;
					if(w.ShowDialog() == true && vm.EdittingList.Any()) {
						//save
						HistoryIO.SaveRoots(vm.EdittingList.Min(), vm.EdittingList.Max());
					}
				});
				MenuList.Add(new MenuItemVm(vc) { Header = "アカウント追加" });
			}else if(ty == NodeType.Total) {
				var vc = new ViewModelCommand(() => {
					var vm = new NodeNameEditerVM(model, new BrokerNode());
					var w = new Views.NodeNameEditWindow();
					w.DataContext = vm;
					if(w.ShowDialog() == true && vm.EdittingList.Any()) {
						//save
						HistoryIO.SaveRoots(vm.EdittingList.Min(), vm.EdittingList.Max());
					}
				});
				MenuList.Add(new MenuItemVm(vc) { Header = "ブローカー追加" });
			}

			var vmc = new ViewModelCommand(() => {
				var vm = new NodeNameEditerVM(model.Parent, model);
				var w = new Views.NodeNameEditWindow();
				w.DataContext = vm;
				if(w.ShowDialog()==true && vm.EdittingList.Any()) {
					//save
					HistoryIO.SaveRoots(vm.EdittingList.Min(), vm.EdittingList.Max());
				}
			}, () => model.Parent != null);
			MenuList.Add(new MenuItemVm(vmc) { Header = "名前の変更" });
			
			if(ty == NodeType.Broker || ty == NodeType.Account) {
				var vc = new ViewModelCommand(() => {
					
					var lst = new double[] { model.Amount, model.InvestmentValue }
						.Concat(model.Levelorder().OfType<FinancialProduct>().Select(a => (double)a.Quantity))
						.Concat(model.Levelorder().OfType<FinancialProduct>().Select(a => (double)a.TradeQuantity));
					if (lst.All(a => a == 0)) {
						var d = this.Model.Upstream().OfType<TotalRiskFundNode>().LastOrDefault()?.CurrentDate;
						this.Model.Parent.RemoveChild(this.Model);
						HistoryIO.SaveRoots((DateTime)d);
					}else {
						MessageBox.Show("ポジションまたは取引に関するデータを保持しているため削除できません","削除不可",MessageBoxButton.OK,MessageBoxImage.Information);
					}

				});
				MenuList.Add(new MenuItemVm(vc) { Header = "削除" });
			}
		}
		protected override void ReCalc() {
			base.ReCalc();
			reculc();
		}
		void reculc() {
			this.ProfitLoss = Model.Amount - InvestmentTotal - InvestmentReturnTotal;
			//this.UnrealizedProfitLoss = Children.OfType<FinancialBasketVM>().Sum(a => a.UnrealizedProfitLoss);
			//this.UnrealizedPLRatio = Model.Amount != 0 ? UnrealizedProfitLoss / Model.Amount * 100 : 0;
			OnPropertyChanged(nameof(UnrealizedProfitLoss));
			OnPropertyChanged(nameof(UnrealizedPLRatio));
		}
		long _pl;
		/// <summary>PL</summary>
		public long ProfitLoss {
			get { return _pl; }
			set { SetProperty(ref _pl, value); }
		}
		/// <summary>含み損益</summary>
		public virtual long UnrealizedProfitLoss
			=> Children.OfType<_FinancialBasketVM>().Sum(a => a.UnrealizedProfitLoss);

		/// <summary>含み損益率</summary>
		public double UnrealizedPLRatio
			=> Model.Amount != 0 ? ((double)UnrealizedProfitLoss / (double)(Model.Amount)) * 100 : 0;
	}
}
