import type { ReactNode } from 'react'
import { cn } from '@/shared/utils'

export function Label({
  className,
  ...props
}: React.LabelHTMLAttributes<HTMLLabelElement>) {
  return (
    <label
      className={cn(
        'text-xs font-semibold leading-none peer-disabled:cursor-not-allowed peer-disabled:opacity-70 text-zinc-700 dark:text-zinc-300',
        className
      )}
      {...props}
    />
  )
}

interface FormFieldProps {
  label: ReactNode
  rightAction?: ReactNode
  error?: string
  required?: boolean
  children: ReactNode
  className?: string
}

export function FormField({
  label,
  rightAction,
  error,
  required,
  children,
  className,
}: FormFieldProps) {
  return (
    <div className={cn('space-y-2', className)}>
      <div className="flex items-center justify-between gap-2">
        <Label className="inline-flex items-center gap-1">
          <span>{label}</span>
          {required && <span className="text-destructive font-bold text-xs leading-none">*</span>}
        </Label>
        {rightAction && <div className="text-xs">{rightAction}</div>}
      </div>
      {children}
      {error && <p className="text-xs text-destructive mt-1">{error}</p>}
    </div>
  )
}
