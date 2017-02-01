﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PortFolion.Core;
using System.Windows.Input;
using Houzkin.Architecture;
using System.ComponentModel;

namespace PortFolion.ViewModels {
	public class FinancialValueVM : CommonNodeVM {
		public FinancialValueVM(FinancialValue model) : base(model) {
		}
		
	}
	public class FinancialProductVM : FinancialValueVM {
		public FinancialProductVM(FinancialValue model) : base(model) {
		}
		protected override void Refresh() {
			base.Refresh();
			OnPropertyChanged(nameof(PerPriceAverage));
			OnPropertyChanged(nameof(UnrealizedProfitLoss));
			OnPropertyChanged(nameof(UnrealizedProfitLossRate));
			OnPropertyChanged(nameof(CurrentPerPrice));
		}
		#region 試
		Dictionary<string, IEnumerable<string>> propRaiseChain = new Dictionary<string, IEnumerable<string>>();
		void propCng(object sender, PropertyChangedEventArgs e) {
			foreach(var prc in propRaiseChain) {
				
			}
		}
		#endregion
		/// <summary>平均取引コスト</summary>
		public double PerPriceAverage {
			get {
				return MaybeModelAs<FinancialProduct>().TrueOrNot(
					o => ((InvestmentTotal - InvestmentReturnTotal) / o.Quantity),
					x => 0);
			}
		}
		/// <summary>含み</summary>
		public long UnrealizedProfitLoss {
			get {
				return (Model.Amount - InvestmentTotal + InvestmentReturnTotal);
			}
		}
		/// <summary>含み率</summary>
		public double? UnrealizedProfitLossRate {
			get {
				var b = (InvestmentTotal - InvestmentReturnTotal);
				if (b == 0) return null;
				return UnrealizedProfitLoss / b * 100;
			}
		}
		/// <summary>現在単価</summary>
		public double CurrentPerPrice {
			get {
				return MaybeModelAs<FinancialProduct>().TrueOrNot(
					o => o.Amount / o.Quantity,
					x => 0);
			}
			set {
				var fp = Model as FinancialProduct;
				if (fp == null) return;
				fp.SetAmount((long)(fp.Quantity * value));
			}
		}
		[ReflectReferenceValue]
		public long Quantity {
			get {
				return MaybeModelAs<FinancialProduct>().TrueOrNot(
					o => o.Quantity,
					x => 0);
			}
			set {
				var fp = Model as FinancialProduct;
				if (fp == null || this.CurrentPerPrice == 0 || value == 0) return;
				if(!IsBlocking(nameof(Quantity)))
					fp.SetQuantity(value);
				fp.SetAmount((long)(value * CurrentPerPrice));
			}
		}
	}
}
