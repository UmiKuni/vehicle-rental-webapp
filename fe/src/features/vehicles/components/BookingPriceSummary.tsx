interface BookingPriceSummaryProps {
  totalDays: number
  rentalAmount: number
  deliveryFee: number
  depositAmount: number
  totalAmount: number
}

export function BookingPriceSummary({
  totalDays,
  rentalAmount,
  deliveryFee,
  depositAmount,
  totalAmount,
}: BookingPriceSummaryProps) {
  return (
    <div className="rounded-2xl border border-border bg-muted/30 p-3 space-y-1.5 text-[11px]">
      <div className="flex justify-between text-muted-foreground">
        <span>Đơn giá thuê ({totalDays} ngày)</span>
        <span>{new Intl.NumberFormat('vi-VN').format(rentalAmount)}đ</span>
      </div>
      {deliveryFee > 0 && (
        <div className="flex justify-between text-muted-foreground">
          <span>Phí giao xe tận nơi</span>
          <span>{new Intl.NumberFormat('vi-VN').format(deliveryFee)}đ</span>
        </div>
      )}
      <div className="flex justify-between text-muted-foreground pt-1 border-t border-border/50">
        <span>Tiền thế chấp (hoàn lại)</span>
        <span className="font-medium text-foreground">
          {new Intl.NumberFormat('vi-VN').format(depositAmount)}đ
        </span>
      </div>
      <div className="flex justify-between text-sm font-bold text-foreground pt-1 border-t border-border">
        <span>Tổng thanh toán</span>
        <span className="text-[#1a8a9d] dark:text-[#22d3ee]">
          {new Intl.NumberFormat('vi-VN').format(totalAmount)}đ
        </span>
      </div>
    </div>
  )
}
