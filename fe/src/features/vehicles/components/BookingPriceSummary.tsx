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
        <span>Rental rate ({totalDays} {totalDays > 1 ? 'days' : 'day'})</span>
        <span>{new Intl.NumberFormat('en-US').format(rentalAmount)} VND</span>
      </div>
      {deliveryFee > 0 && (
        <div className="flex justify-between text-muted-foreground">
          <span>Doorstep delivery fee</span>
          <span>{new Intl.NumberFormat('en-US').format(deliveryFee)} VND</span>
        </div>
      )}
      <div className="flex justify-between text-muted-foreground pt-1 border-t border-border/50">
        <span>Security deposit (refundable)</span>
        <span className="font-medium text-foreground">
          {new Intl.NumberFormat('en-US').format(depositAmount)} VND
        </span>
      </div>
      <div className="flex justify-between text-sm font-bold text-foreground pt-1 border-t border-border">
        <span>Total Payment</span>
        <span className="text-[#1a8a9d] dark:text-[#22d3ee]">
          {new Intl.NumberFormat('en-US').format(totalAmount)} VND
        </span>
      </div>
    </div>
  )
}
