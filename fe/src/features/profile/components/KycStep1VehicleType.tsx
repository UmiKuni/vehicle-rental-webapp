import { useForm, Controller } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { kycStep1Schema, type KycStep1Values } from '../schemas/kyc.schema'
import type { VehicleInterest, LicenseClass } from '@/types/kyc.types'
import { cn } from '@/shared/utils'
import { Button } from '@/shared/components/ui/Button'
import { ChevronRight, Bike, Car, Truck, Zap, Car as CarIcon } from 'lucide-react'

interface Props {
  defaultValues?: Partial<KycStep1Values>
  onNext: (data: KycStep1Values) => void
}

const VEHICLE_OPTIONS: { value: VehicleInterest; label: string; icon: typeof Bike; desc: string }[] = [
  { value: 'MOTORBIKE', label: 'Xe máy', icon: Bike, desc: 'Mô tô, xe ga, xe số' },
  { value: 'CAR_4_SEAT', label: 'Ô tô 4 chỗ', icon: CarIcon, desc: 'Sedan, Hatchback, Coupe' },
  { value: 'CAR_7_SEAT', label: 'Ô tô 7 chỗ', icon: Car, desc: 'SUV, Minivan, MPV' },
  { value: 'TRUCK', label: 'Xe tải', icon: Truck, desc: 'Xe tải nhỏ, pick-up' },
  { value: 'ELECTRIC', label: 'Xe điện', icon: Zap, desc: 'EV, Hybrid' },
]

const LICENSE_CLASSES: { value: LicenseClass; label: string; desc: string }[] = [
  { value: 'A1', label: 'A1', desc: 'Mô tô ≤ 175cc' },
  { value: 'A2', label: 'A2', desc: 'Mô tô > 175cc' },
  { value: 'B1', label: 'B1', desc: 'Ô tô ≤ 9 chỗ (không hành nghề)' },
  { value: 'B2', label: 'B2', desc: 'Ô tô ≤ 9 chỗ (hành nghề)' },
  { value: 'C', label: 'C', desc: 'Xe tải' },
  { value: 'D', label: 'D', desc: 'Xe khách ≤ 30 chỗ' },
  { value: 'E', label: 'E', desc: 'Xe khách > 30 chỗ' },
]

export function KycStep1VehicleType({ defaultValues, onNext }: Props) {
  const {
    control,
    handleSubmit,
    watch,
    formState: { errors },
  } = useForm<KycStep1Values>({
    resolver: zodResolver(kycStep1Schema),
    defaultValues: {
      vehicleInterests: [],
      hasLicense: true,
      ...defaultValues,
    },
  })

  const selectedInterests = watch('vehicleInterests')
  const hasLicense = watch('hasLicense')

  return (
    <form onSubmit={handleSubmit(onNext)} className="space-y-6">
      {/* Vehicle type multi-select */}
      <div className="space-y-3">
        <div>
          <p className="text-sm font-semibold text-foreground">Loại phương tiện quan tâm</p>
          <p className="text-xs text-muted-foreground mt-0.5">Chọn một hoặc nhiều loại</p>
        </div>

        <Controller
          name="vehicleInterests"
          control={control}
          render={({ field }) => (
            <div className="grid grid-cols-1 gap-2">
              {VEHICLE_OPTIONS.map(({ value, label, icon: Icon, desc }) => {
                const selected = field.value.includes(value)
                return (
                  <button
                    key={value}
                    type="button"
                    onClick={() => {
                      const next = selected
                        ? field.value.filter((v) => v !== value)
                        : [...field.value, value]
                      field.onChange(next)
                    }}
                    className={cn(
                      'flex items-center gap-3 rounded-xl border p-3.5 text-left transition-all active-press',
                      selected
                        ? 'border-[#1a8a9d] bg-[#1a8a9d]/8 dark:bg-[#1a8a9d]/15 ring-1 ring-[#1a8a9d]/30'
                        : 'border-border bg-card hover:bg-accent'
                    )}
                  >
                    <div
                      className={cn(
                        'flex h-9 w-9 shrink-0 items-center justify-center rounded-lg',
                        selected ? 'bg-[#1a8a9d] text-white' : 'bg-muted text-muted-foreground'
                      )}
                    >
                      <Icon className="size-4" />
                    </div>
                    <div className="min-w-0 flex-1">
                      <p className={cn('text-sm font-semibold', selected ? 'text-[#1a8a9d]' : 'text-foreground')}>
                        {label}
                      </p>
                      <p className="text-xs text-muted-foreground">{desc}</p>
                    </div>
                    <div
                      className={cn(
                        'h-4 w-4 shrink-0 rounded border-2 transition-all',
                        selected ? 'bg-[#1a8a9d] border-[#1a8a9d]' : 'border-border'
                      )}
                    >
                      {selected && (
                        <svg viewBox="0 0 12 12" className="size-full fill-white">
                          <path d="M2 6l3 3 5-5" stroke="white" strokeWidth="1.5" fill="none" strokeLinecap="round" strokeLinejoin="round" />
                        </svg>
                      )}
                    </div>
                  </button>
                )
              })}
            </div>
          )}
        />
        {errors.vehicleInterests && (
          <p className="text-xs text-destructive">{errors.vehicleInterests.message}</p>
        )}
      </div>

      {/* Has license toggle */}
      <div className="space-y-3">
        <div>
          <p className="text-sm font-semibold text-foreground">Bạn có bằng lái xe không?</p>
        </div>
        <Controller
          name="hasLicense"
          control={control}
          render={({ field }) => (
            <div className="flex gap-3">
              {[{ value: true, label: 'Có bằng lái' }, { value: false, label: 'Chưa có' }].map(
                ({ value, label }) => (
                  <button
                    key={String(value)}
                    type="button"
                    onClick={() => field.onChange(value)}
                    className={cn(
                      'flex-1 rounded-xl border py-2.5 text-sm font-semibold transition-all',
                      field.value === value
                        ? 'border-[#1a8a9d] bg-[#1a8a9d] text-white shadow-sm'
                        : 'border-border bg-card text-muted-foreground hover:bg-accent'
                    )}
                  >
                    {label}
                  </button>
                )
              )}
            </div>
          )}
        />
      </div>

      {/* License class — only if has license */}
      {hasLicense && (
        <div className="space-y-3">
          <div>
            <p className="text-sm font-semibold text-foreground">Hạng bằng lái</p>
          </div>
          <Controller
            name="licenseClass"
            control={control}
            render={({ field }) => (
              <div className="flex flex-wrap gap-2">
                {LICENSE_CLASSES.map(({ value, label, desc }) => (
                  <button
                    key={value}
                    type="button"
                    onClick={() => field.onChange(value)}
                    title={desc}
                    className={cn(
                      'rounded-lg border px-3.5 py-2 text-sm font-bold transition-all active-press',
                      field.value === value
                        ? 'border-[#1a8a9d] bg-[#1a8a9d] text-white shadow-sm'
                        : 'border-border bg-card text-foreground hover:bg-accent'
                    )}
                  >
                    {label}
                  </button>
                ))}
              </div>
            )}
          />
          {errors.licenseClass && (
            <p className="text-xs text-destructive">{errors.licenseClass.message}</p>
          )}
        </div>
      )}

      {/* Selected summary */}
      {selectedInterests.length > 0 && (
        <div className="rounded-xl bg-muted/40 border border-border p-3.5 text-xs text-muted-foreground">
          <span className="font-semibold text-foreground">Đã chọn:</span>{' '}
          {selectedInterests
            .map((i) => VEHICLE_OPTIONS.find((o) => o.value === i)?.label)
            .join(', ')}
        </div>
      )}

      <Button
        type="submit"
        className="w-full h-12 rounded-xl bg-[#1a8a9d] hover:bg-[#147485] text-white font-semibold text-sm flex items-center justify-center gap-2 active-press"
      >
        Tiếp theo
        <ChevronRight className="size-4" />
      </Button>
    </form>
  )
}
